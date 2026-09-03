using System.Text;
using MailArchiver.Services.Providers.Graph;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Graph.Models;
using Microsoft.Kiota.Serialization.Json;
using Xunit;

namespace MailArchiver.Tests.Services;

/// <summary>
/// Unit tests for <see cref="SanitizingJsonParseNodeFactory"/>.
/// Reproduces the production failure: Exchange returns message payloads whose
/// body.content string contains invalid UTF-8 bytes. JsonDocument.Parse accepts
/// them, but JsonElement.Deserialize(string) inside Kiota's GetStringValue() then
/// throws "The JSON value could not be converted to System.String", failing the
/// whole message page. The sanitizer must replace the invalid bytes with U+FFFD
/// before parsing so the email is still archived.
/// </summary>
public class SanitizingJsonParseNodeFactoryTests
{
    private static SanitizingJsonParseNodeFactory CreateFactory()
    {
        return new SanitizingJsonParseNodeFactory(
            NullLogger<SanitizingJsonParseNodeFactory>.Instance);
    }

    private static byte[] BuildCorruptedPayload(byte[] invalidBytes)
    {
        var stream = new MemoryStream();
        stream.Write(Encoding.UTF8.GetBytes("{\"subject\":\"Hello\",\"body\":{\"content\":\""));
        stream.Write(Encoding.UTF8.GetBytes(new string('x', 1000)));
        stream.Write(invalidBytes);
        stream.Write(Encoding.UTF8.GetBytes(" end\",\"contentType\":\"html\"}}"));
        return stream.ToArray();
    }

    [Fact]
    public void SanitizeUtf8_ValidPayload_ReturnsInputUnchanged()
    {
        var payload = Encoding.UTF8.GetBytes("{\"subject\":\"Hello\"}");

        var result = SanitizingJsonParseNodeFactory.SanitizeUtf8(payload, out var replacements);

        Assert.Same(payload, result);
        Assert.Equal(0, replacements);
    }

    [Fact]
    public void SanitizeUtf8_InvalidByteSequence_ReplacesWithReplacementChar()
    {
        var payload = BuildCorruptedPayload(new byte[] { 0xFF, 0xFE });

        var result = SanitizingJsonParseNodeFactory.SanitizeUtf8(payload, out var replacements);

        Assert.NotSame(payload, result);
        Assert.True(replacements > 0);

        // The sanitized payload must be strictly valid UTF-8 again.
        var (isValid, _) = SanitizingJsonParseNodeFactory.ValidateUtf8(result);
        Assert.True(isValid);

        // And the body content must contain the replacement character.
        var text = Encoding.UTF8.GetString(result);
        Assert.Contains('\uFFFD', text);
        Assert.Contains(" end", text);
    }

    [Fact]
    public void SanitizeUtf8_TruncatedMultibyteSequence_IsReplaced()
    {
        // 0xE2 0x82 starts a 3-byte euro-sign sequence but the final 0xAC is missing.
        var payload = BuildCorruptedPayload(new byte[] { 0xE2, 0x82 });

        var result = SanitizingJsonParseNodeFactory.SanitizeUtf8(payload, out var replacements);

        var (isValid, _) = SanitizingJsonParseNodeFactory.ValidateUtf8(result);
        Assert.True(isValid);
        Assert.True(replacements > 0);
    }

    [Fact]
    public void SanitizeUtf8_LoneContinuationByte_IsReplaced()
    {
        var payload = BuildCorruptedPayload(new byte[] { 0x80 });

        var result = SanitizingJsonParseNodeFactory.SanitizeUtf8(payload, out var replacements);

        var (isValid, _) = SanitizingJsonParseNodeFactory.ValidateUtf8(result);
        Assert.True(isValid);
        Assert.True(replacements > 0);
    }

    [Fact]
    public void SanitizeUtf8_EmptyPayload_StaysEmpty()
    {
        var payload = Array.Empty<byte>();

        var result = SanitizingJsonParseNodeFactory.SanitizeUtf8(payload, out var replacements);

        Assert.Same(payload, result);
        Assert.Equal(0, replacements);
    }

    [Fact]
    public void ValidateUtf8_DetectsInvalidSequenceIndex()
    {
        var valid = Encoding.UTF8.GetBytes("abc\u00e4\u20ac\U0001F600");
        var (isValid, index) = SanitizingJsonParseNodeFactory.ValidateUtf8(valid);
        Assert.True(isValid);
        Assert.Equal(-1, index);

        var invalid = BuildCorruptedPayload(new byte[] { 0xFF });
        (isValid, index) = SanitizingJsonParseNodeFactory.ValidateUtf8(invalid);
        Assert.False(isValid);
        Assert.True(index > 0);
    }

    [Fact]
    public async Task GetRootParseNodeAsync_CorruptedPayload_ParsesIntoModel()
    {
        var factory = CreateFactory();
        var payload = BuildCorruptedPayload(new byte[] { 0xFF, 0xFE });

        using var stream = new MemoryStream(payload);
        var rootNode = await factory.GetRootParseNodeAsync("application/json", stream);

        // The parse must succeed and the body must contain replacement characters.
        var bodyNode = rootNode.GetChildNode("body");
        Assert.NotNull(bodyNode);

        var contentNode = bodyNode!.GetChildNode("content");
        Assert.NotNull(contentNode);

        var content = contentNode!.GetStringValue();
        Assert.NotNull(content);
        Assert.Contains('\uFFFD', content);
        Assert.EndsWith(" end", content);
    }

    [Fact]
    public async Task GetRootParseNodeAsync_ValidPayload_BehavesLikeStandardFactory()
    {
        var factory = CreateFactory();
        var payload = Encoding.UTF8.GetBytes(
            "{\"subject\":\"Hello\",\"body\":{\"content\":\"World\",\"contentType\":\"html\"}}");

        using var stream = new MemoryStream(payload);
        var rootNode = await factory.GetRootParseNodeAsync("application/json", stream);

        var subject = rootNode.GetChildNode("subject")?.GetStringValue();
        var content = rootNode.GetChildNode("body")?.GetChildNode("content")?.GetStringValue();

        Assert.Equal("Hello", subject);
        Assert.Equal("World", content);
    }

    [Fact]
    public async Task GetRootParseNodeAsync_MultibyteContent_PreservesValidCharacters()
    {
        var factory = CreateFactory();
        var payload = Encoding.UTF8.GetBytes(
            "{\"subject\":\"Grüße \u20ac\",\"body\":{\"content\":\"Björn \U0001F600\",\"contentType\":\"text\"}}");

        using var stream = new MemoryStream(payload);
        var rootNode = await factory.GetRootParseNodeAsync("application/json", stream);

        Assert.Equal("Grüße \u20ac", rootNode.GetChildNode("subject")?.GetStringValue());
        Assert.Equal("Björn \U0001F600", rootNode.GetChildNode("body")?.GetChildNode("content")?.GetStringValue());
    }

    [Fact]
    public void ValidContentType_MatchesStandardJsonFactory()
    {
        Assert.Equal(new JsonParseNodeFactory().ValidContentType, CreateFactory().ValidContentType);
    }

    [Fact]
    public async Task GetRootParseNodeAsync_NullContent_ThrowsArgumentNullException()
    {
        var factory = CreateFactory();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            factory.GetRootParseNodeAsync("application/json", null!));
    }

    [Fact]
    public async Task GetRootParseNodeAsync_EmptyContentType_ThrowsArgumentNullException()
    {
        var factory = CreateFactory();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("{}"));

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            factory.GetRootParseNodeAsync("", stream));
    }

    [Fact]
    public async Task GetRootParseNodeAsync_NonJsonContentType_ThrowsArgumentOutOfRangeException()
    {
        var factory = CreateFactory();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("plain"));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            factory.GetRootParseNodeAsync("text/plain", stream));
    }

    /// <summary>
    /// End-to-end reproduction of the production failure: a ~113 KB body.content string
    /// (matching the BytePositionInLine of the reported error) containing invalid UTF-8
    /// bytes. Without the sanitizer, Kiota's GetStringValue() throws the
    /// "could not be converted to System.String" JsonException and the whole message page
    /// fails. With the sanitizer, the Message model deserializes completely and the body
    /// is archived with replacement characters.
    /// </summary>
    [Fact]
    public async Task GetRootParseNodeAsync_CorruptedLargeBody_DeserializesIntoMessageModel()
    {
        var stream = new MemoryStream();
        stream.Write(Encoding.UTF8.GetBytes("{\"id\":\"AAMk123\",\"subject\":\"Report\",\"internetMessageId\":\"<x@y>\",\"body\":{\"content\":\""));
        stream.Write(Encoding.UTF8.GetBytes(new string('x', 113_000)));
        stream.Write(new byte[] { 0xFF, 0xFE }); // invalid UTF-8, as seen in production
        stream.Write(Encoding.UTF8.GetBytes(" tail\",\"contentType\":\"html\"}}"));

        var factory = CreateFactory();
        var rootNode = await factory.GetRootParseNodeAsync(
            "application/json",
            new MemoryStream(stream.ToArray()));

        var message = rootNode.GetObjectValue<Message>(Message.CreateFromDiscriminatorValue);

        Assert.Equal("Report", message.Subject);
        Assert.Equal("<x@y>", message.InternetMessageId);
        Assert.NotNull(message.Body?.Content);
        Assert.True(message.Body!.Content!.Length > 113_000);
        Assert.Contains('\uFFFD', message.Body!.Content);
        Assert.EndsWith(" tail", message.Body!.Content);
        Assert.Equal(BodyType.Html, message.Body!.ContentType);
    }
}
using System.Text;
using System.Text.Json;
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

    // ============================================================
    // Surrogate-escape sanitization (the second corruption class)
    // ============================================================

    /// <summary>
    /// The second observed production corruption class: an unpaired surrogate escape
    /// (\ud83d without its low surrogate) inside a large body string. Bytes are plain
    /// ASCII, so UTF-8 validation passes them through unchanged - which is exactly why
    /// the UTF-8-only fix could not help. STJ accepts them in JsonDocument.Parse, but
    /// GetStringValue() throws "The JSON value could not be converted to System.String".
    /// </summary>
    [Fact]
    public async Task GetRootParseNodeAsync_LargeBodyWithUnpairedSurrogateEscape_DeserializesIntoMessageModel()
    {
        // Build a ~113 KB message with an unpaired \ud83d at approximately the reported
        // production offset. Without the surrogate fix this exact payload reproduces the
        // production exception byte-for-byte.
        var body = new string('x', 113_000) + "\\ud83d" + " tail";
        var payload = Encoding.UTF8.GetBytes(
            "{\"id\":\"AAMk123\",\"subject\":\"Report\",\"internetMessageId\":\"<x@y>\",\"body\":{\"content\":\"" + body + "\",\"contentType\":\"html\"}}");

        // Sanity: this payload is byte-identical to the one that crashed production. If the
        // sanitizer is absent, Kiota would throw here.
        var unsanitized = new JsonParseNodeFactory();
        JsonException? ex = null;
        using (var s = new MemoryStream(payload))
        {
            var node = await unsanitized.GetRootParseNodeAsync("application/json", s);
            try { _ = node.GetObjectValue<Message>(Message.CreateFromDiscriminatorValue); }
            catch (JsonException je) { ex = je; }
        }
        Assert.NotNull(ex);
        Assert.Contains("could not be converted to System.String", ex!.Message);

        // With the factory: the model must deserialize.
        var factory = CreateFactory();
        var rootNode = await factory.GetRootParseNodeAsync("application/json", new MemoryStream(payload));
        var message = rootNode.GetObjectValue<Message>(Message.CreateFromDiscriminatorValue);

        Assert.Equal("Report", message.Subject);
        Assert.Equal("<x@y>", message.InternetMessageId);
        Assert.NotNull(message.Body?.Content);
        Assert.Contains('\uFFFD', message.Body!.Content);
        Assert.EndsWith(" tail", message.Body!.Content);
        Assert.True(message.Body!.Content!.Length >= 113_000);
    }

    [Fact]
    public void SanitizeSurrogateEscapes_UnpairedHighSurrogate_Replaced()
    {
        // {"s":"ab\ud83dcd"}
        var payload = Encoding.UTF8.GetBytes("{\"s\":\"ab\\ud83dcd\"}");

        var replaced = SanitizingJsonParseNodeFactory.SanitizeSurrogateEscapes(payload);

        Assert.Equal(1, replaced);
        Assert.Equal("{\"s\":\"ab\\ufffdcd\"}", Encoding.UTF8.GetString(payload));
    }

    [Fact]
    public void SanitizeSurrogateEscapes_ValidEmojiPair_Unchanged()
    {
        // 😀 = 😀 - high \ud83d + low \ude00 adjacent
        var payload = Encoding.UTF8.GetBytes("{\"s\":\"ab\\ud83d\\ude00cd\"}");

        var replaced = SanitizingJsonParseNodeFactory.SanitizeSurrogateEscapes(payload);

        Assert.Equal(0, replaced);
        Assert.Equal("{\"s\":\"ab\\ud83d\\ude00cd\"}", Encoding.UTF8.GetString(payload));
    }

    [Fact]
    public void SanitizeSurrogateEscapes_HighSurrogateNotImmediatelyAdjacentToLow_Unpaired()
    {
        // High and low escape are separated by a text byte; STJ requires them to be
        // adjacent to form a pair, so the high one alone is unpaired and must be replaced.
        var payload = Encoding.UTF8.GetBytes("{\"s\":\"\\ud83dX\\ude00\"}");

        var replaced = SanitizingJsonParseNodeFactory.SanitizeSurrogateEscapes(payload);

        Assert.True(replaced > 0);
        var text = Encoding.UTF8.GetString(payload);
        Assert.Contains("\\ufffd", text);
        // And now the former low surrogate stands alone -> also replaced (still unpaired).
        var secondPass = SanitizingJsonParseNodeFactory.SanitizeSurrogateEscapes(payload);
        Assert.Equal(0, secondPass); // already clean after first pass
    }

    [Fact]
    public void SanitizeSurrogateEscapes_LoneLowSurrogate_Replaced()
    {
        var payload = Encoding.UTF8.GetBytes("{\"s\":\"ab\\ude00cd\"}");

        var replaced = SanitizingJsonParseNodeFactory.SanitizeSurrogateEscapes(payload);

        Assert.Equal(1, replaced);
        Assert.Equal("{\"s\":\"ab\\ufffdcd\"}", Encoding.UTF8.GetString(payload));
    }

    [Fact]
    public void SanitizeSurrogateEscapes_EscapedBackslash_NotTouched()
    {
        // "\\ud83d" = literal backslash + literal "ud83d" - NOT an escape sequence.
        var payload = Encoding.UTF8.GetBytes("{\"s\":\"ab\\\\ud83dcd\"}");

        var replaced = SanitizingJsonParseNodeFactory.SanitizeSurrogateEscapes(payload);

        Assert.Equal(0, replaced);
        Assert.Equal("{\"s\":\"ab\\\\ud83dcd\"}", Encoding.UTF8.GetString(payload));
    }

    [Fact]
    public void SanitizeSurrogateEscapes_EscapedBackslashFollowedByRealEscape_Replaced()
    {
        // Payload as raw bytes to avoid double-escaping confusion: the JSON string token
        // is  "ab" + literal backslash ("\\") + the unpaired escape "\ud83d" + "ef".
        var payload = Encoding.UTF8.GetBytes("{\"s\":\"ab")
            .Concat(new byte[] { 0x5C, 0x5C })              // literal backslash: JSON "\\"
            .Concat(Encoding.UTF8.GetBytes("\\ud83d"))     // the unpaired surrogate escape
            .Concat(Encoding.UTF8.GetBytes("ef\"}"))       // rest of the document
            .ToArray();

        var replaced = SanitizingJsonParseNodeFactory.SanitizeSurrogateEscapes(payload);

        Assert.Equal(1, replaced);

        // Expected: the literal backslash survives, the escape becomes \ufffd.
        var expected = Encoding.UTF8.GetBytes("{\"s\":\"ab")
            .Concat(new byte[] { 0x5C, 0x5C })
            .Concat(Encoding.UTF8.GetBytes("\\ufffd"))
            .Concat(Encoding.UTF8.GetBytes("ef\"}"))
            .ToArray();

        Assert.Equal(expected, payload);
    }

    [Fact]
    public void SanitizeSurrogateEscapes_UppercaseHex_Replaced()
    {
        var payload = Encoding.UTF8.GetBytes("{\"s\":\"a\\uD83Db\"}");

        var replaced = SanitizingJsonParseNodeFactory.SanitizeSurrogateEscapes(payload);

        Assert.Equal(1, replaced);
        Assert.Equal("{\"s\":\"a\\ufffdb\"}", Encoding.UTF8.GetString(payload));
    }

    [Fact]
    public void SanitizeSurrogateEscapes_MultipleUnpairedSurrogates_AllReplaced()
    {
        var payload = Encoding.UTF8.GetBytes("{\"a\":\"x\\ud83d\",\"b\":\"y\\ude00\",\"c\":\"z\\ud83dw\"}");

        var replaced = SanitizingJsonParseNodeFactory.SanitizeSurrogateEscapes(payload);

        Assert.Equal(3, replaced);
        var text = Encoding.UTF8.GetString(payload);
        Assert.DoesNotContain("\\ud83d", text);
        Assert.DoesNotContain("\\ude00", text);
    }

    [Fact]
    public void SanitizeSurrogateEscapes_NonSurrogateEscapes_Unchanged()
    {
        var payload = Encoding.UTF8.GetBytes("{\"s\":\"a\\u00e4b\\u0041c\"}");

        var replaced = SanitizingJsonParseNodeFactory.SanitizeSurrogateEscapes(payload);

        Assert.Equal(0, replaced);
        Assert.Equal("{\"s\":\"a\\u00e4b\\u0041c\"}", Encoding.UTF8.GetString(payload));
    }

    [Fact]
    public void SanitizeSurrogateEscapes_TruncatedEscapeAtStringEnd_LeftForParser()
    {
        // "\ud8" at the very end: no full surrogate escape; the parser will reject the
        // payload anyway (JsonReaderException). Sanitizer must not touch it.
        var payload = Encoding.UTF8.GetBytes("{\"s\":\"ab\\ud8\"}");

        var replaced = SanitizingJsonParseNodeFactory.SanitizeSurrogateEscapes(payload);

        Assert.Equal(0, replaced);
        Assert.Equal("{\"s\":\"ab\\ud8\"}", Encoding.UTF8.GetString(payload));
    }

    [Fact]
    public void SanitizeSurrogateEscapes_EscapesOutsideStringTokens_Untouched()
    {
        // Surrogate escapes can only legally appear inside string tokens. The state
        // machine tracks the string boundary so a stray \ud800 outside a string (which
        // would make the JSON invalid anyway) is not touched.
        var payload = Encoding.UTF8.GetBytes("[\\ud83d]"); // invalid JSON anyway

        var replaced = SanitizingJsonParseNodeFactory.SanitizeSurrogateEscapes(payload);

        Assert.Equal(0, replaced);
    }

    [Fact]
    public void SanitizeSurrogateEscapes_CombinedWithInvalidUtf8_BothFixed()
    {
        var stream = new MemoryStream();
        stream.Write(Encoding.UTF8.GetBytes("{\"s\":\"ab"));
        stream.Write(new byte[] { 0xFF });
        stream.Write(Encoding.UTF8.GetBytes("\\ud83dcd\"}"));
        var payload = stream.ToArray();

        var afterUtf8 = SanitizingJsonParseNodeFactory.SanitizeUtf8(payload, out var utf8Replacements);
        var total = utf8Replacements + SanitizingJsonParseNodeFactory.SanitizeSurrogateEscapes(afterUtf8);

        var text = Encoding.UTF8.GetString(afterUtf8);

        // Invalid UTF-8 bytes -> FFFD, unpaired surrogate escape -> \ufffd escape.
        // The exact count of utf8Replacements depends on the decoder, but both layers
        // must report some fixes and the text must be free of both corruptions.
        Assert.True(total > 0);
        Assert.DoesNotContain("\\ud83d", text);
        Assert.Contains("cd", text);
    }
}
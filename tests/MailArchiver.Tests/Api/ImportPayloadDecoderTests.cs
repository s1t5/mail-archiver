using MailArchiver.Models.Api;
using System.Text;
using Xunit;

namespace MailArchiver.Tests.Api;

/// <summary>
/// Unit tests for <see cref="ImportPayloadDecoder"/> — the base64/utf8 handling
/// of POST /api/v1/emails.
/// </summary>
public class ImportPayloadDecoderTests
{
    private const string Mime = "From: a@example.com\r\nTo: b@example.com\r\nSubject: Test\r\n\r\nHello";

    [Fact]
    public void TryDecode_Base64_ReturnsBytes()
    {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(Mime));

        var ok = ImportPayloadDecoder.TryDecode(encoded, "base64", out var bytes, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal(Encoding.UTF8.GetBytes(Mime), bytes);
    }

    [Fact]
    public void TryDecode_Utf8_ReturnsBytes()
    {
        var ok = ImportPayloadDecoder.TryDecode(Mime, "utf8", out var bytes, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal(Mime, Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public void TryDecode_DefaultsToBase64()
    {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(Mime));

        var ok = ImportPayloadDecoder.TryDecode(encoded, null, out var bytes, out _);

        Assert.True(ok);
        Assert.Equal(Encoding.UTF8.GetBytes(Mime), bytes);
    }

    [Fact]
    public void TryDecode_UnknownEncoding_IsRejected()
    {
        var ok = ImportPayloadDecoder.TryDecode(Mime, "rot13", out _, out var error);

        Assert.False(ok);
        Assert.Contains("base64", error);
    }

    [Fact]
    public void TryDecode_InvalidBase64_IsRejected()
    {
        var ok = ImportPayloadDecoder.TryDecode("not base64 !!", "base64", out _, out var error);

        Assert.False(ok);
        Assert.Contains("not valid base64", error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryDecode_MissingContent_IsRejected(string? content)
    {
        var ok = ImportPayloadDecoder.TryDecode(content, "base64", out _, out var error);

        Assert.False(ok);
        Assert.Contains("required", error);
    }
}

using MailArchiver.Services.Shared;
using Xunit;

namespace MailArchiver.Tests.Shared;

/// <summary>
/// Unit tests for <see cref="MailContentHelper.NormalizeMessageId"/>.
/// Covers canonical, malformed, and edge-case Message-ID header values
/// observed in real-world IMAP traffic, in particular the doubled-closing-bracket
/// defect reported in the retention-deletion bug.
/// </summary>
public class MailContentHelperNormalizeMessageIdTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("\t\n\r", "")]
    public void Normalize_NullOrWhitespace_ReturnsEmpty(string? input, string expected)
    {
        Assert.Equal(expected, MailContentHelper.NormalizeMessageId(input));
    }

    [Theory]
    [InlineData("<id@host>", "id@host")]
    [InlineData("id@host", "id@host")]
    [InlineData("<id@host", "id@host")]
    [InlineData("id@host>", "id@host")]
    [InlineData("<id@host>>", "id@host")]
    [InlineData("<<id@host>>", "id@host")]
    [InlineData("<id@host> ", "id@host")]
    [InlineData(" <id@host> ", "id@host")]
    [InlineData("<a.b+c@d.e>", "a.b+c@d.e")]
    [InlineData("<id@host>\r\n", "id@host")]
    public void Normalize_MalformedBrackets_StripsToBareToken(string input, string expected)
    {
        Assert.Equal(expected, MailContentHelper.NormalizeMessageId(input));
    }

    [Theory]
    [InlineData("<>", "")]
    [InlineData("<<>>", "")]
    public void Normalize_OnlyBracketsAndWhitespace_ReturnsEmpty(string input, string expected)
    {
        Assert.Equal(expected, MailContentHelper.NormalizeMessageId(input));
    }

    [Fact]
    public void Normalize_BugReportExample_MatchesArchive()
    {
        // Exact example from the bug report: raw IMAP header with doubled closing bracket.
        var rawFromImap = "<231101124718.507@example.info>>";
        var storedInArchive = "231101124718.507@example.info";

        var normalized = MailContentHelper.NormalizeMessageId(rawFromImap);

        Assert.Equal(storedInArchive, normalized);
    }

    [Theory]
    [InlineData("<id@host>")]
    [InlineData("id@host")]
    [InlineData("<id@host>>")]
    [InlineData("<<id@host>>")]
    [InlineData(" <id@host> ")]
    [InlineData("<a.b+c@d.e>")]
    [InlineData("")]
    [InlineData("<>")]
    public void Normalize_IsIdempotent(string input)
    {
        var once = MailContentHelper.NormalizeMessageId(input);
        var twice = MailContentHelper.NormalizeMessageId(once);
        Assert.Equal(once, twice);
    }
}
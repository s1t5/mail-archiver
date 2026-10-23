using MailArchiver.Services.Shared;
using MimeKit;
using System.Text;
using Xunit;

namespace MailArchiver.Tests.Shared;

/// <summary>
/// Unit tests for <see cref="MailContentHelper.CleanText"/>.
/// </summary>
public class MailContentHelperCleanTextTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    public void CleanText_NullOrEmpty_ReturnsEmpty(string? input, string expected)
        => Assert.Equal(expected, MailContentHelper.CleanText(input));

    [Fact]
    public void CleanText_RemovesNullBytes()
    {
        var input = "hello\0world";
        Assert.Equal("helloworld", MailContentHelper.CleanText(input));
    }

    [Fact]
    public void CleanText_ReplacesControlCharsWithSpaceExceptCrLnTab()
    {
        var input = "a\u0001b\u0007c\rd\ne\tf";
        var result = MailContentHelper.CleanText(input);
        Assert.Equal("a b c\rd\ne\tf", result);
    }

    [Fact]
    public void CleanText_PreservesPrintableAsciiAndUnicode()
    {
        var input = "Hello, 世界! €50";
        Assert.Equal(input, MailContentHelper.CleanText(input));
    }

    [Fact]
    public void CleanText_PreservesTabsNewlinesCarriageReturns()
    {
        var input = "line1\r\nline2\tindented";
        Assert.Equal(input, MailContentHelper.CleanText(input));
    }
}

/// <summary>
/// Unit tests for <see cref="MailContentHelper.IsHtmlContent"/>.
/// </summary>
public class MailContentHelperIsHtmlContentTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void IsHtmlContent_NullOrEmpty_ReturnsFalse(string? text)
        => Assert.False(MailContentHelper.IsHtmlContent(text, "<html></html>"));

    [Fact]
    public void IsHtmlContent_EqualToHtmlBody_ReturnsTrue()
    {
        var html = "<html><body>Hi</body></html>";
        Assert.True(MailContentHelper.IsHtmlContent(html, html));
    }

    [Theory]
    [InlineData("<!doctype html>")]
    [InlineData("<html>")]
    [InlineData("<HTML>")]
    [InlineData("<head>")]
    [InlineData("<body>")]
    [InlineData("  <body>")]
    public void IsHtmlContent_StartsWithMarkupMarker_ReturnsTrue(string text)
        => Assert.True(MailContentHelper.IsHtmlContent(text, "other"));

    [Theory]
    [InlineData("Hello world")]
    [InlineData("   plain text")]
    public void IsHtmlContent_PlainText_ReturnsFalse(string text)
        => Assert.False(MailContentHelper.IsHtmlContent(text, "different html"));

    [Fact]
    public void IsHtmlContent_EmptyAfterTrim_ReturnsFalse()
        => Assert.False(MailContentHelper.IsHtmlContent("   ", null));
}

/// <summary>
/// Unit tests for <see cref="MailContentHelper.RemoveNullBytes"/>.
/// </summary>
public class MailContentHelperRemoveNullBytesTests
{
    [Fact]
    public void RemoveNullBytes_Null_ReturnsNull()
        => Assert.Null(MailContentHelper.RemoveNullBytes(null));

    [Theory]
    [InlineData("")]
    [InlineData("no nulls here")]
    public void RemoveNullBytes_NoNullBytes_ReturnsInput(string input)
        => Assert.Equal(input, MailContentHelper.RemoveNullBytes(input));

    [Fact]
    public void RemoveNullBytes_WithNullBytes_StripsThem()
        => Assert.Equal("ab", MailContentHelper.RemoveNullBytes("a\0\0b"));
}

/// <summary>
/// Unit tests for <see cref="MailContentHelper.TruncateFieldForTsvector"/> and
/// <see cref="MailContentHelper.TruncateTextForStorage"/>.
/// </summary>
public class MailContentHelperTruncateTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void TruncateFieldForTsvector_NullOrEmpty_ReturnsEmpty(string? input)
        => Assert.Equal(string.Empty, MailContentHelper.TruncateFieldForTsvector(input, 100));

    [Fact]
    public void TruncateFieldForTsvector_UnderLimit_ReturnsVerbatim()
    {
        var input = "short text";
        Assert.Equal(input, MailContentHelper.TruncateFieldForTsvector(input, 1000));
    }

    [Fact]
    public void TruncateFieldForTsvector_OverLimit_TruncatesAndAppendsEllipsis()
    {
        var input = new string('a', 5000);
        var result = MailContentHelper.TruncateFieldForTsvector(input, 100);
        Assert.EndsWith("...", result);
        Assert.True(result.Length < input.Length + 3);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void TruncateTextForStorage_NullOrEmpty_ReturnsEmpty(string? input)
        => Assert.Equal(string.Empty, MailContentHelper.TruncateTextForStorage(input, 1000));

    [Fact]
    public void TruncateTextForStorage_UnderLimit_ReturnsVerbatim()
    {
        var input = "plain content";
        Assert.Equal(input, MailContentHelper.TruncateTextForStorage(input, 1000));
    }

    [Fact]
    public void TruncateTextForStorage_OverLimit_AppendsTruncationNotice()
    {
        var input = new string('x', 10_000);
        var result = MailContentHelper.TruncateTextForStorage(input, 1000);
        Assert.Contains("[CONTENT TRUNCATED", result);
        Assert.True(Encoding.UTF8.GetByteCount(result) <= 1000);
    }

    [Fact]
    public void TruncateTextForStorage_NoticeOverheadExceedsLimit_ReturnsOnlyNotice()
    {
        var result = MailContentHelper.TruncateTextForStorage(new string('x', 10), 50);
        Assert.Contains("[CONTENT TRUNCATED", result);
    }

    [Fact]
    public void TruncateTextForStorage_MultibyteUtf8_DoesNotProduceInvalidUtf8()
    {
        var input = new string('€', 10_000);
        var result = MailContentHelper.TruncateTextForStorage(input, 500);
        var bytes = Encoding.UTF8.GetBytes(result);
        Assert.True(bytes.Length <= 500);
    }
}

/// <summary>
/// Unit tests for <see cref="MailContentHelper.SanitizeLongTokens"/>.
/// </summary>
public class MailContentHelperSanitizeLongTokensTests
{
    [Fact]
    public void Sanitize_Null_ReturnsNull()
        => Assert.Null(MailContentHelper.SanitizeLongTokens(null));

    [Theory]
    [InlineData("")]
    public void Sanitize_EmptyString_ReturnsEmpty(string input)
        => Assert.Equal(string.Empty, MailContentHelper.SanitizeLongTokens(input));

    [Fact]
    public void Sanitize_WhitespaceOnly_ReturnsWhitespaceUnchanged()
        => Assert.Equal(" ", MailContentHelper.SanitizeLongTokens(" "));

    [Fact]
    public void Sanitize_TokensUnderLimit_ReturnsVerbatim()
    {
        var input = "hello world foo bar";
        Assert.Equal(input, MailContentHelper.SanitizeLongTokens(input, 2047));
    }

    [Fact]
    public void Sanitize_LongToken_GetsBrokenUp()
    {
        var input = new string('a', 5000);
        var result = MailContentHelper.SanitizeLongTokens(input, 1000);
        var firstSpace = result.IndexOf(' ');
        Assert.True(firstSpace > 0 && firstSpace <= 1000);
        Assert.True(result.Length > input.Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Sanitize_NonPositiveLimit_ReturnsInputUnchanged(int limit)
        => Assert.Equal("anything", MailContentHelper.SanitizeLongTokens("anything", limit));

    [Fact]
    public void Sanitize_PreservesWhitespaceBetweenTokens()
    {
        var input = "a\n\nb";
        Assert.Equal(input, MailContentHelper.SanitizeLongTokens(input, 2047));
    }

    [Fact]
    public void Sanitize_MixedTokens_OnlyBreaksLongOnes()
    {
        var shortTok = "short";
        var longTok = new string('b', 4000);
        var input = $"{shortTok} {longTok} {shortTok}";
        var result = MailContentHelper.SanitizeLongTokens(input, 1000);
        Assert.Contains(shortTok, result);
        Assert.True(result.Length > input.Length);
        Assert.Contains(" ", result);
    }
}

/// <summary>
/// Unit tests for <see cref="MailContentHelper.CleanHtmlForStorage"/>.
/// </summary>
public class MailContentHelperCleanHtmlForStorageTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void CleanHtml_NullOrEmpty_ReturnsEmpty(string? html)
        => Assert.Equal(string.Empty, MailContentHelper.CleanHtmlForStorage(html));

    [Fact]
    public void CleanHtml_SmallHtml_ReturnsVerbatim()
    {
        var html = "<html><body><p>hi</p></body></html>";
        Assert.Equal(html, MailContentHelper.CleanHtmlForStorage(html));
    }

    [Fact]
    public void CleanHtml_RemovesNullBytes()
    {
        var html = "<html>\0<body>hi</body></html>";
        var result = MailContentHelper.CleanHtmlForStorage(html);
        Assert.False(result.Contains('\0'), "result must not contain null bytes");
    }

    [Fact]
    public void CleanHtml_LargeHtml_GetsTruncatedWithNotice()
    {
        var html = "<html><body>" + new string('x', 2_000_000) + "</body></html>";
        var result = MailContentHelper.CleanHtmlForStorage(html);
        Assert.Contains("truncated", result, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.Length < html.Length);
    }
}

/// <summary>
/// Unit tests for inline image / extension helpers in <see cref="MailContentHelper"/>.
/// </summary>
public class MailContentHelperInlineImagesTests
{
    [Fact]
    public void ResolveInlineImages_NullOrEmpty_ReturnsInput()
    {
        Assert.Equal("", MailContentHelper.ResolveInlineImagesInHtml("", new List<Models.EmailAttachment>()));
        Assert.Equal(null, MailContentHelper.ResolveInlineImagesInHtml(null!, new List<Models.EmailAttachment>()));
    }

    [Fact]
    public void ResolveInlineImages_NoMatchingAttachment_ReturnsOriginalHtml()
    {
        var html = "<img src=\"cid:missing\">";
        var result = MailContentHelper.ResolveInlineImagesInHtml(html, new List<Models.EmailAttachment>());
        Assert.Equal(html, result);
    }

    [Fact]
    public void ResolveInlineImages_MatchingByContentId_ConvertsToDataUrl()
    {
        var content = new byte[] { 1, 2, 3 };
        var attachments = new List<Models.EmailAttachment>
        {
            new() { ContentId = "<img1>", ContentType = "image/png", Content = content }
        };
        var html = "<img src=\"cid:img1\">";
        var result = MailContentHelper.ResolveInlineImagesInHtml(html, attachments);
        Assert.Contains("data:image/png;base64,", result);
    }

    [Fact]
    public void ResolveInlineImages_MatchingByFileName_ConvertsToDataUrl()
    {
        var content = new byte[] { 4, 5 };
        var attachments = new List<Models.EmailAttachment>
        {
            new() { FileName = "inline_img1.png", ContentType = "image/png", Content = content }
        };
        var html = "<img src=\"cid:img1\">";
        var result = MailContentHelper.ResolveInlineImagesInHtml(html, attachments);
        Assert.Contains("data:image/png;base64,", result);
    }

    [Fact]
    public void ProcessHtmlBodyForInlineImages_AssignsContentIdWhenMissing()
    {
        var attachments = new List<Models.EmailAttachment>
        {
            new() { FileName = "inline_img1.png", ContentId = "" }
        };
        var html = "<img src=\"cid:img1\">";
        var result = MailContentHelper.ProcessHtmlBodyForInlineImages(html, attachments);
        Assert.Contains("cid:", result);
        Assert.True(attachments[0].ContentId!.StartsWith("<"));
    }

    [Fact]
    public void ProcessHtmlBodyForInlineImages_NullHtml_ReturnsInput()
        => Assert.Equal(null, MailContentHelper.ProcessHtmlBodyForInlineImages(null!, new List<Models.EmailAttachment>()));
}

/// <summary>
/// Unit tests for <see cref="MailContentHelper.IsGraphInlineContent"/> and
/// <see cref="MailContentHelper.GetExtensionFromContentType"/>.
/// </summary>
public class MailContentHelperGraphInlineAndExtensionTests
{
    [Theory]
    [InlineData("abc", null, null, true)]
    [InlineData("", "image/png", null, true)]
    [InlineData("", "text/plain", null, false)]
    [InlineData(null, null, null, false)]
    public void IsGraphInlineContent_Variants(string? contentId, string? contentType, string? fileName, bool expected)
        => Assert.Equal(expected, MailContentHelper.IsGraphInlineContent(contentId, contentType, fileName));

    [Theory]
    [InlineData("image/png", ".png")]
    [InlineData("image/jpeg", ".jpg")]
    [InlineData("image/jpg", ".jpg")]
    [InlineData("image/gif", ".gif")]
    [InlineData("image/bmp", ".bmp")]
    [InlineData("image/tiff", ".tiff")]
    [InlineData("image/svg+xml", ".svg")]
    [InlineData("image/webp", ".webp")]
    [InlineData("text/html", ".html")]
    [InlineData("text/plain", ".txt")]
    [InlineData("application/pdf", ".pdf")]
    [InlineData("application/octet-stream", ".dat")]
    [InlineData(null, ".dat")]
    [InlineData("unknown/x", ".dat")]
    public void GetExtensionFromContentType_Mapping(string? contentType, string expected)
        => Assert.Equal(expected, MailContentHelper.GetExtensionFromContentType(contentType));
}

/// <summary>
/// Unit tests for <see cref="MailContentHelper.ApplyDisplayNames"/>.
/// </summary>
public class MailContentHelperApplyDisplayNamesTests
{
    [Fact]
    public void ApplyDisplayNames_CountMatches_AssignsNames()
    {
        var list = new InternetAddressList
        {
            new MailboxAddress("", "a@x.com"),
            new MailboxAddress("", "b@x.com")
        };
        MailContentHelper.ApplyDisplayNames(list, "Alice, Bob");
        Assert.Equal("Alice", ((MailboxAddress)list[0]).Name);
        Assert.Equal("Bob", ((MailboxAddress)list[1]).Name);
    }

    [Fact]
    public void ApplyDisplayNames_CountMismatch_LeavesNamesUnchanged()
    {
        var list = new InternetAddressList { new MailboxAddress("", "a@x.com") };
        MailContentHelper.ApplyDisplayNames(list, "Alice, Bob");
        Assert.Equal("", ((MailboxAddress)list[0]).Name);
    }

    [Fact]
    public void ApplyDisplayNames_NullOrEmpty_NoOp()
    {
        var list = new InternetAddressList { new MailboxAddress("Original", "a@x.com") };
        MailContentHelper.ApplyDisplayNames(list, null);
        Assert.Equal("Original", ((MailboxAddress)list[0]).Name);
    }

    [Fact]
    public void ApplyDisplayNames_EmptyNameEntry_NotOverwritten()
    {
        var list = new InternetAddressList
        {
            new MailboxAddress("Keep", "a@x.com"),
            new MailboxAddress("", "b@x.com")
        };
        MailContentHelper.ApplyDisplayNames(list, "Alice, ");
        Assert.Equal("Alice", ((MailboxAddress)list[0]).Name);
        Assert.Equal("", ((MailboxAddress)list[1]).Name);
    }
}
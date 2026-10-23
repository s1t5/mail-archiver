using MailArchiver.Services.Shared;
using Xunit;

namespace MailArchiver.Tests.Shared;

/// <summary>
/// Unit tests for <see cref="MarkdownHelper.ToHtml"/>.
/// </summary>
public class MarkdownHelperTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ToHtml_NullOrWhitespace_ReturnsEmpty(string? input)
        => Assert.Equal(string.Empty, MarkdownHelper.ToHtml(input!));

    [Theory]
    [InlineData("# Heading", "<h1>Heading</h1>")]
    [InlineData("## Sub", "<h2>Sub</h2>")]
    [InlineData("### Deep", "<h3>Deep</h3>")]
    public void ToHtml_Headings(string input, string expected)
        => Assert.Contains(expected, MarkdownHelper.ToHtml(input));

    [Theory]
    [InlineData("**bold**", "<strong>bold</strong>")]
    [InlineData("__bold__", "<strong>bold</strong>")]
    [InlineData("*italic*", "<em>italic</em>")]
    [InlineData("_italic_", "<em>italic</em>")]
    [InlineData("~~strike~~", "<del>strike</del>")]
    public void ToHtml_InlineFormatting(string input, string expected)
        => Assert.Contains(expected, MarkdownHelper.ToHtml(input));

    [Fact]
    public void ToHtml_BoldItalic_Combined()
        => Assert.Contains("<strong><em>both</em></strong>", MarkdownHelper.ToHtml("***both***"));

    [Fact]
    public void ToHtml_InlineCode_HtmlEncoded()
    {
        var result = MarkdownHelper.ToHtml("`<script>`");
        Assert.Contains("<code>&lt;script&gt;</code>", result);
    }

    [Fact]
    public void ToHtml_CodeBlock_HtmlEncoded()
    {
        var result = MarkdownHelper.ToHtml("```\n<x>\n```");
        Assert.Contains("<pre><code>", result);
        Assert.Contains("&lt;x&gt;", result);
    }

    [Fact]
    public void ToHtml_CodeBlockWithLanguage_AddsLanguageClass()
    {
        var result = MarkdownHelper.ToHtml("```csharp\nvar x = 1;\n```");
        Assert.Contains("class=\"language-csharp\"", result);
    }

    [Theory]
    [InlineData("- a\n- b", "<ul>", "</ul>")]
    [InlineData("1. a\n2. b", "<ol>", "</ol>")]
    public void ToHtml_Lists(string input, string openTag, string closeTag)
    {
        var result = MarkdownHelper.ToHtml(input);
        Assert.Contains(openTag, result);
        Assert.Contains(closeTag, result);
        Assert.Contains("<li>a</li>", result);
        Assert.Contains("<li>b</li>", result);
    }

    [Fact]
    public void ToHtml_Blockquote()
    {
        var result = MarkdownHelper.ToHtml("> quoted text");
        Assert.Contains("<blockquote>", result);
        Assert.Contains("quoted text", result);
    }

    [Theory]
    [InlineData("---")]
    [InlineData("***")]
    [InlineData("___")]
    public void ToHtml_HorizontalRule(string rule)
        => Assert.Contains("<hr>", MarkdownHelper.ToHtml(rule));

    [Fact]
    public void ToHtml_Link()
        => Assert.Contains("<a href=\"https://x.com\">text</a>", MarkdownHelper.ToHtml("[text](https://x.com)"));

    [Fact]
    public void ToHtml_Image()
    {
        var result = MarkdownHelper.ToHtml("![alt](https://x.com/i.png)");
        Assert.Contains("<img src=\"https://x.com/i.png\" alt=\"alt\">", result);
    }

    [Fact]
    public void ToHtml_Paragraph()
        => Assert.Contains("<p>Just text</p>", MarkdownHelper.ToHtml("Just text"));

    [Fact]
    public void ToHtml_MultipleBlocks()
    {
        var input = "# Title\n\nParagraph";
        var result = MarkdownHelper.ToHtml(input);
        Assert.Contains("<h1>Title</h1>", result);
        Assert.Contains("<p>Paragraph</p>", result);
    }

    [Fact]
    public void ToHtml_ListClosesOnEmptyLine()
    {
        var input = "- a\n\nParagraph";
        var result = MarkdownHelper.ToHtml(input);
        Assert.Contains("</ul>", result);
        Assert.Contains("<p>Paragraph</p>", result);
    }
}
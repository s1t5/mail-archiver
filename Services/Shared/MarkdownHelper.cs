using System.Text;
using System.Text.RegularExpressions;

namespace MailArchiver.Services.Shared
{
    /// <summary>
    /// Simple, lightweight Markdown-to-HTML converter.
    /// No external dependencies — avoids licensing issues with third-party libraries like Markdig.
    /// Supports the subset of Markdown commonly found in GitHub release notes:
    /// headings, bold, italic, inline code, code blocks, unordered/ordered lists,
    /// links, images, blockquotes, horizontal rules, and paragraphs.
    /// </summary>
    public static class MarkdownHelper
    {
        /// <summary>
        /// Converts a Markdown string to HTML.
        /// </summary>
        public static string ToHtml(string markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown))
                return string.Empty;

            var sb = new StringBuilder(markdown);
            var html = new StringBuilder();

            // Normalise line endings
            var text = sb.Replace("\r\n", "\n").Replace("\r", "\n").ToString();

            // Extract and protect code blocks first (ticks ``` ... ```)
            var codeBlocks = new List<string>();
            text = Regex.Replace(text, @"```(\w*)\n([\s\S]*?)```", match =>
            {
                var lang = match.Groups[1].Value;
                var code = System.Net.WebUtility.HtmlEncode(match.Groups[2].Value);
                var block = string.IsNullOrEmpty(lang)
                    ? $"<pre><code>{code}</code></pre>"
                    : $"<pre><code class=\"language-{lang}\">{code}</code></pre>";
                codeBlocks.Add(block);
                return $"\u0001CODEBLOCK{codeBlocks.Count - 1}\u0001";
            });

            // Split into lines and process block-level elements
            var lines = text.Split('\n');
            var inList = false;
            var listType = ""; // "ul" or "ol"

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var trimmed = line.Trim();

                // Empty line — close any open list
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    if (inList)
                    {
                        html.AppendLine(listType == "ol" ? "</ol>" : "</ul>");
                        inList = false;
                    }
                    continue;
                }

                // Heading (# ## ###)
                var headingMatch = Regex.Match(trimmed, @"^(#{1,6})\s+(.+)$");
                if (headingMatch.Success)
                {
                    if (inList) { html.AppendLine(listType == "ol" ? "</ol>" : "</ul>"); inList = false; }
                    var level = headingMatch.Groups[1].Length;
                    var content = FormatInline(headingMatch.Groups[2].Value);
                    html.AppendLine($"<h{level}>{content}</h{level}>");
                    continue;
                }

                // Horizontal rule (---, ***, ___)
                if (Regex.IsMatch(trimmed, @"^[-*_]{3,}$"))
                {
                    if (inList) { html.AppendLine(listType == "ol" ? "</ol>" : "</ul>"); inList = false; }
                    html.AppendLine("<hr>");
                    continue;
                }

                // Blockquote (> text)
                if (trimmed.StartsWith(">"))
                {
                    if (inList) { html.AppendLine(listType == "ol" ? "</ol>" : "</ul>"); inList = false; }
                    var content = FormatInline(trimmed.Substring(1).Trim());
                    html.AppendLine($"<blockquote><p>{content}</p></blockquote>");
                    continue;
                }

                // Unordered list item (- or * followed by space)
                var ulMatch = Regex.Match(trimmed, @"^[-*]\s+(.+)$");
                if (ulMatch.Success)
                {
                    if (!inList || listType != "ul")
                    {
                        if (inList) html.AppendLine(listType == "ol" ? "</ol>" : "</ul>");
                        html.AppendLine("<ul>");
                        inList = true;
                        listType = "ul";
                    }
                    html.AppendLine($"<li>{FormatInline(ulMatch.Groups[1].Value)}</li>");
                    continue;
                }

                // Ordered list item (1. text)
                var olMatch = Regex.Match(trimmed, @"^\d+\.\s+(.+)$");
                if (olMatch.Success)
                {
                    if (!inList || listType != "ol")
                    {
                        if (inList) html.AppendLine(listType == "ol" ? "</ol>" : "</ul>");
                        html.AppendLine("<ol>");
                        inList = true;
                        listType = "ol";
                    }
                    html.AppendLine($"<li>{FormatInline(olMatch.Groups[1].Value)}</li>");
                    continue;
                }

                // Regular paragraph text
                if (inList)
                {
                    html.AppendLine(listType == "ol" ? "</ol>" : "</ul>");
                    inList = false;
                }
                html.AppendLine($"<p>{FormatInline(trimmed)}</p>");
            }

            // Close any still-open list
            if (inList)
            {
                html.AppendLine(listType == "ol" ? "</ol>" : "</ul>");
            }

            // Restore protected code blocks
            var result = html.ToString();
            for (int j = 0; j < codeBlocks.Count; j++)
            {
                result = result.Replace($"\u0001CODEBLOCK{j}\u0001", codeBlocks[j]);
            }

            return result.Trim();
        }

        /// <summary>
        /// Formats inline elements: bold, italic, strikethrough, inline code, links, and images.
        /// </summary>
        private static string FormatInline(string text)
        {
            // Images first: ![alt](url)
            text = Regex.Replace(text, @"!\[([^\]]*)\]\(([^)]+)\)", "<img src=\"$2\" alt=\"$1\">");

            // Links: [text](url)
            text = Regex.Replace(text, @"\[([^\]]+)\]\(([^)]+)\)", "<a href=\"$2\">$1</a>");

            // Bold + Italic: ***text*** or ___text___
            text = Regex.Replace(text, @"\*\*\*(.+?)\*\*\*", "<strong><em>$1</em></strong>");
            text = Regex.Replace(text, @"___(.+?)___", "<strong><em>$1</em></strong>");

            // Bold: **text** or __text__
            text = Regex.Replace(text, @"\*\*(.+?)\*\*", "<strong>$1</strong>");
            text = Regex.Replace(text, @"__(.+?)__", "<strong>$1</strong>");

            // Italic: *text* or _text_
            text = Regex.Replace(text, @"(?<!\*)\*([^*]+)\*(?!\*)", "<em>$1</em>");
            text = Regex.Replace(text, @"(?<!_)_([^_]+)_(?!_)", "<em>$1</em>");

            // Strikethrough: ~~text~~
            text = Regex.Replace(text, @"~~(.+?)~~", "<del>$1</del>");

            // Inline code: `text`
            text = Regex.Replace(text, @"`([^`]+)`", match =>
            {
                var code = System.Net.WebUtility.HtmlEncode(match.Groups[1].Value);
                return $"<code>{code}</code>";
            });

            return text;
        }
    }
}
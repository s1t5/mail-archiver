namespace MailArchiver.Models
{
    /// <summary>
    /// Configuration for the read-only MCP (Model Context Protocol) server, bound from
    /// the "Mcp" section. The MCP server is disabled by default — a safe default for
    /// upstream. It mirrors the REST API: same API keys, same account-access scoping,
    /// same read-only email access via EmailCoreService.
    /// </summary>
    public class McpOptions
    {
        public const string Mcp = "Mcp";

        /// <summary>Master switch. When false, the /mcp endpoint behaves as if it does not exist (404).</summary>
        public bool Enabled { get; set; } = false;

        /// <summary>When false, the get_attachment tool returns an error instead of attachment bytes.</summary>
        public bool AllowAttachmentDownloads { get; set; } = true;

        /// <summary>Upper bound for the number of emails returned by search_emails (page size clamp).</summary>
        public int MaxResults { get; set; } = 100;

        /// <summary>Default page size used by search_emails when the caller omits the pageSize argument.</summary>
        public int DefaultPageSize { get; set; } = 20;

        /// <summary>Maximum attachment size (in bytes) the get_attachment tool will return inline.</summary>
        public long MaxAttachmentBytes { get; set; } = 10_000_000;

        /// <summary>Fixed-window request budget per API key per minute.</summary>
        public int RateLimitPerMinute { get; set; } = 120;
    }
}
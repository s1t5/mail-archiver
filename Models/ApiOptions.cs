namespace MailArchiver.Models
{
    /// <summary>
    /// Configuration for the read-only REST API, bound from the "Api" section.
    /// The API is disabled by default — a safe default for upstream.
    /// </summary>
    public class ApiOptions
    {
        public const string Api = "Api";

        /// <summary>Master switch. When false, all /api/* routes return 404.</summary>
        public bool Enabled { get; set; } = false;

        /// <summary>When false, the attachment download endpoint returns 403.</summary>
        public bool AllowAttachmentDownloads { get; set; } = true;

        /// <summary>When true (and Enabled), mounts Swagger UI and the OpenAPI document.</summary>
        public bool EnableSwaggerUi { get; set; } = true;

        /// <summary>Page size used when a request omits pageSize.</summary>
        public int DefaultPageSize { get; set; } = 20;

        /// <summary>Upper bound for pageSize; larger values are clamped to this.</summary>
        public int MaxPageSize { get; set; } = 100;

        /// <summary>Fixed-window request budget per API key per minute.</summary>
        public int RateLimitPerMinute { get; set; } = 120;
    }
}

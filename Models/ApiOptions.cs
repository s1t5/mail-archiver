namespace MailArchiver.Models
{
    /// <summary>
    /// Configuration for the REST API, bound from the "Api" section.
    /// The API is disabled by default — a safe default for upstream. Read
    /// endpoints are always available once <see cref="Enabled"/> is true; the
    /// write endpoints additionally require <see cref="AllowWrites"/> and an
    /// API key that carries the <c>write</c> scope.
    /// </summary>
    public class ApiOptions
    {
        public const string Api = "Api";

        /// <summary>Master switch. When false, all /api/* routes return 404.</summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Master switch for every non-GET endpoint (POST/PUT/PATCH/DELETE).
        /// When false the write routes answer 403 even for a write-scoped key,
        /// so a deployment that only ever needed read access cannot be changed
        /// by accident. Default: false.
        /// </summary>
        public bool AllowWrites { get; set; } = false;

        /// <summary>
        /// Allows <c>DELETE /api/v1/accounts/{id}</c>. Deleting an account also
        /// deletes every archived message of that account, which is why it is a
        /// separate switch from <see cref="AllowWrites"/>. Default: false.
        /// </summary>
        public bool AllowAccountDeletion { get; set; } = false;

        /// <summary>
        /// Allows <c>DELETE /api/v1/emails/{id}</c>. Deleting a single archived
        /// message is irreversible, so it stays off until explicitly enabled.
        /// Default: false.
        /// </summary>
        public bool AllowEmailDeletion { get; set; } = false;

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

        /// <summary>
        /// Upper bound for the raw MIME payload accepted by
        /// <c>POST /api/v1/emails</c>, in bytes. Default: 25 MB.
        /// </summary>
        public int MaxImportBytes { get; set; } = 25_000_000;
    }
}

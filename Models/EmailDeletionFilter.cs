namespace MailArchiver.Models
{
    /// <summary>
    /// Criteria of a filter-based deletion job. Built from the search form when the
    /// job is queued. <see cref="AllowedAccountIds"/> always comes from the server,
    /// never from the request body.
    /// </summary>
    public class EmailDeletionFilter
    {
        public string? SearchTerm { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int? AccountId { get; set; }
        public string? FolderName { get; set; }
        public bool? IsOutgoing { get; set; }
        public List<int>? AllowedAccountIds { get; set; }

        /// <summary>
        /// Highest matching email id at the time the job was queued. Every batch is
        /// capped at this id, so mail that arrives while the job runs (and therefore
        /// has a higher id) is never deleted by a confirmation that did not include it.
        /// </summary>
        public int? UpperBoundId { get; set; }

        /// <summary>Short human-readable description for the job list and audit log.</summary>
        public string Description { get; set; } = string.Empty;
    }
}

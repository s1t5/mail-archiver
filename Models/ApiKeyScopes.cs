namespace MailArchiver.Models
{
    /// <summary>
    /// The scopes an API key can carry. A key is stored with a comma-separated
    /// scope list (e.g. <c>read</c> or <c>read,write</c>); <c>read</c> is the
    /// default and matches the historical read-only behaviour, so keys created
    /// before scopes existed keep working unchanged.
    /// </summary>
    public static class ApiKeyScopes
    {
        /// <summary>Read access to accounts, folders, messages and attachments.</summary>
        public const string Read = "read";

        /// <summary>
        /// Write access: create/update accounts, import and move messages, and
        /// manage the caller's own API keys. Destructive operations additionally
        /// require the matching <c>Api</c> configuration switch.
        /// </summary>
        public const string Write = "write";

        /// <summary>All known scopes, in canonical storage order.</summary>
        public static readonly IReadOnlyList<string> All = new[] { Read, Write };

        /// <summary>
        /// Claim type carrying one granted scope per claim on an authenticated
        /// API-key principal. Read with <c>User.HasClaim(ClaimType, scope)</c>.
        /// </summary>
        public const string ClaimType = "scope";

        /// <summary>The scope list a key gets when none was supplied.</summary>
        public const string Default = Read;

        /// <summary>
        /// Parses a stored scope list into a set. Unknown tokens are ignored so a
        /// key created by a newer version never breaks an older one. A null or
        /// empty value yields <c>read</c> only.
        /// </summary>
        public static IReadOnlySet<string> Parse(string? raw)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);

            if (!string.IsNullOrWhiteSpace(raw))
            {
                foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var scope = part.ToLowerInvariant();
                    if (All.Contains(scope))
                    {
                        result.Add(scope);
                    }
                }
            }

            if (result.Count == 0)
            {
                result.Add(Read);
            }

            return result;
        }

        /// <summary>True when the stored scope list contains the write scope.</summary>
        public static bool HasWrite(string? raw) => Parse(raw).Contains(Write);

        /// <summary>
        /// Validates a caller-supplied scope list. Returns false with a message
        /// when a scope is unknown or the list is empty.
        /// </summary>
        public static bool TryNormalize(IEnumerable<string>? scopes, out string normalized, out string? error)
        {
            normalized = string.Empty;
            error = null;

            var requested = (scopes ?? Enumerable.Empty<string>())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim().ToLowerInvariant())
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (requested.Count == 0)
            {
                error = $"At least one scope is required. Valid scopes: {string.Join(", ", All)}.";
                return false;
            }

            var unknown = requested.Where(s => !All.Contains(s)).ToList();
            if (unknown.Count > 0)
            {
                error = $"Unknown scope(s): {string.Join(", ", unknown)}. Valid scopes: {string.Join(", ", All)}.";
                return false;
            }

            // Canonical order: read before write, independent of input order.
            normalized = string.Join(",", All.Where(requested.Contains));
            return true;
        }
    }
}

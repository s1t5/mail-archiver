namespace MailArchiver.Models.Api;

/// <summary>
/// Body of <c>POST /api/v1/emails</c>. The message is stored in the archive
/// only; nothing is written back to the IMAP server.
/// </summary>
public class ImportEmailRequest
{
    /// <summary>Target account. Must be visible to the API-key owner.</summary>
    public int AccountId { get; set; }

    /// <summary>Archive folder to store the message in. Defaults to <c>INBOX</c>.</summary>
    public string? Folder { get; set; }

    /// <summary>
    /// The raw RFC 822 message. Base64-encoded by default; set
    /// <see cref="Encoding"/> to <c>utf8</c> to send the message as plain text.
    /// </summary>
    public string? Content { get; set; }

    /// <summary><c>base64</c> (default) or <c>utf8</c>.</summary>
    public string? Encoding { get; set; }
}

/// <summary>Result of a successful import.</summary>
public class ImportEmailResultDto
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public string Folder { get; set; } = string.Empty;
    public string MessageId { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
}

/// <summary>Body of <c>PATCH /api/v1/emails/{id}</c>.</summary>
public class MoveEmailRequest
{
    /// <summary>New archive folder path, e.g. <c>Archiv/2026</c>.</summary>
    public string? FolderName { get; set; }
}

/// <summary>
/// Decodes the <c>content</c> field of an import request. Pure so the encoding
/// rules are unit-testable without a database or a MIME parser.
/// </summary>
public static class ImportPayloadDecoder
{
    public const string Base64 = "base64";
    public const string Utf8 = "utf8";

    public static bool TryDecode(string? content, string? encoding, out byte[] bytes, out string? error)
    {
        bytes = Array.Empty<byte>();
        error = null;

        var normalized = string.IsNullOrWhiteSpace(encoding) ? Base64 : encoding.Trim().ToLowerInvariant();

        if (normalized is not (Base64 or Utf8))
        {
            error = "encoding must be 'base64' or 'utf8'.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            error = "The raw message content is required.";
            return false;
        }

        if (normalized == Base64)
        {
            try
            {
                bytes = Convert.FromBase64String(content);
                return true;
            }
            catch (FormatException)
            {
                error = "content is not valid base64.";
                return false;
            }
        }

        bytes = System.Text.Encoding.UTF8.GetBytes(content);
        return true;
    }
}

/// <summary>
/// Body of <c>POST /api/v1/keys</c>. The plaintext key is returned exactly once.
/// </summary>
public class CreateApiKeyRequest
{
    public string? Name { get; set; }

    /// <summary>Optional expiry (UTC). Omit for a key that never expires.</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Scope list. Defaults to <c>["read"]</c>. Allowed: <c>read</c>, <c>write</c>.
    /// </summary>
    public List<string>? Scopes { get; set; }
}

/// <summary>A stored API key. The key value itself is never returned.</summary>
public class ApiKeyDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty;
    public List<string> Scopes { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public bool IsActive { get; set; }
    public string? OwnerUsername { get; set; }

    public static ApiKeyDto FromEntity(ApiKey key, bool includeOwner = false)
    {
        return new ApiKeyDto
        {
            Id = key.Id,
            Name = key.Name,
            KeyPrefix = key.KeyPrefix,
            Scopes = ApiKeyScopes.Parse(key.Scopes).ToList(),
            CreatedAt = key.CreatedAt,
            LastUsedAt = key.LastUsedAt,
            ExpiresAt = key.ExpiresAt,
            RevokedAt = key.RevokedAt,
            IsActive = key.IsActive,
            OwnerUsername = includeOwner ? key.User?.Username : null
        };
    }
}

/// <summary>
/// Response of <c>POST /api/v1/keys</c>. <see cref="Key"/> is the only time the
/// plaintext key is ever returned.
/// </summary>
public class CreatedApiKeyDto : ApiKeyDto
{
    public string Key { get; set; } = string.Empty;
}

/// <summary>Status of an asynchronous account deletion job.</summary>
public class AccountDeletionJobDto
{
    public string JobId { get; set; } = string.Empty;
    public int AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CurrentPhase { get; set; } = string.Empty;
    public DateTime Created { get; set; }
    public DateTime? Started { get; set; }
    public DateTime? Completed { get; set; }
    public int DeletedEmails { get; set; }
    public int TotalEmails { get; set; }
    public int DeletedAttachments { get; set; }
    public int TotalAttachments { get; set; }
    public string? ErrorMessage { get; set; }

    public static AccountDeletionJobDto FromJob(MailAccountDeletionJob job)
    {
        return new AccountDeletionJobDto
        {
            JobId = job.JobId,
            AccountId = job.MailAccountId,
            AccountName = job.MailAccountName,
            Status = job.Status.ToString(),
            CurrentPhase = job.CurrentPhase,
            Created = job.Created,
            Started = job.Started,
            Completed = job.Completed,
            DeletedEmails = job.DeletedEmails,
            TotalEmails = job.TotalEmails,
            DeletedAttachments = job.DeletedAttachments,
            TotalAttachments = job.TotalAttachments,
            ErrorMessage = job.ErrorMessage
        };
    }
}

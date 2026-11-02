namespace MailArchiver.Models.Api;

/// <summary>
/// Complete-message payload returned by the MCP <c>get_email_eml</c> and <c>get_email_raw</c>
/// tools. MCP results are JSON, so the message travels as text: verbatim when it is valid
/// UTF-8 (the usual case), otherwise base64 so that no byte is altered.
/// </summary>
public class MessageDownloadDto
{
    public int EmailId { get; set; }

    /// <summary>"eml" (rebuilt from the archived fields) or "raw" (original as received).</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>For "raw": imap, graph, eml-file or serialized. Null for "eml".</summary>
    public string? Source { get; set; }

    /// <summary>SHA-256 of the message bytes, lowercase hex.</summary>
    public string Sha256 { get; set; } = string.Empty;

    public long Size { get; set; }

    /// <summary>"utf-8" when <see cref="Content"/> is the message text, "base64" otherwise.</summary>
    public string Encoding { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;
}

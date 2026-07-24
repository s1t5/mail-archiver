namespace MailArchiver.Models.Api;

/// <summary>
/// Attachment payload DTO returned by the MCP <c>get_attachment</c> tool.
/// Carries the attachment metadata plus the raw bytes as a base64 string,
/// because MCP tool results are JSON-serialized and cannot stream binary data.
/// </summary>
public class AttachmentDownloadDto
{
    public int Id { get; set; }
    public int EmailId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Size { get; set; }
    public string Base64Data { get; set; } = string.Empty;
}
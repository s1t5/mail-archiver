using MailArchiver.Controllers.Api;
using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Models.Api;
using MailArchiver.Services;
using MailArchiver.Services.Core;
using MailArchiver.Services.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MimeKit;

namespace MailArchiver.Controllers.Api.V1;

[Route("api/v1/emails")]
public class EmailsApiController : ApiControllerBase
{
    private const int MaxFolderNameLength = 255;

    private readonly MailArchiverDbContext _context;
    private readonly EmailCoreService _emailCoreService;
    private readonly MailImporter _mailImporter;
    private readonly ApiOptions _options;
    private readonly IAccessLogService _accessLogService;

    public EmailsApiController(
        MailArchiverDbContext context,
        EmailCoreService emailCoreService,
        MailImporter mailImporter,
        IOptions<ApiOptions> options,
        IAccessLogService accessLogService)
    {
        _context = context;
        _emailCoreService = emailCoreService;
        _mailImporter = mailImporter;
        _options = options.Value;
        _accessLogService = accessLogService;
    }

    [HttpGet("")]
    public async Task<ActionResult<PagedResultDto<EmailSummaryDto>>> Search(
        [FromQuery] string? q,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? accountId,
        [FromQuery] string? folder,
        [FromQuery] string? direction,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 0,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortOrder = null)
    {
        page = Math.Max(1, page);
        pageSize = pageSize <= 0 ? _options.DefaultPageSize : pageSize;
        pageSize = Math.Clamp(pageSize, 1, _options.MaxPageSize);
        var skip = (page - 1) * pageSize;

        // Direction is still validated locally for a 400 UX; sortBy/sortOrder
        // are canonicalized centrally by EmailCoreService (any unknown value
        // falls back to the defaults, no 400).
        var isOutgoing = ParseDirection(direction);
        if (isOutgoing == DirectionParseResult.Invalid)
        {
            return BadRequest();
        }

        var allowed = await GetAllowedAccountIdsAsync();
        bool? isOutgoingValue = isOutgoing switch
        {
            DirectionParseResult.Incoming => false,
            DirectionParseResult.Outgoing => true,
            _ => null
        };

        var (emails, totalCount) = await _emailCoreService.SearchEmailsAsync(
            q ?? string.Empty,
            from,
            to,
            accountId,
            folder,
            isOutgoingValue,
            skip,
            pageSize,
            allowed,
            sortBy,
            sortOrder);

        var result = new PagedResultDto<EmailSummaryDto>
        {
            Items = emails.Select(EmailSummaryDto.FromEntity).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };

        await _accessLogService.LogAccessAsync(
            CurrentUsername,
            AccessLogType.Search,
            searchParameters: BuildSearchSummary(q, from, to, accountId, folder, direction, page, pageSize, sortBy, sortOrder));

        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<EmailDetailDto>> GetMessage(int id)
    {
        var allowed = await GetAllowedAccountIdsAsync();
        var email = await _context.ArchivedEmails
            .Include(e => e.Attachments)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (email == null || (allowed != null && !allowed.Contains(email.MailAccountId)))
        {
            return NotFound();
        }

        await _accessLogService.LogAccessAsync(
            CurrentUsername,
            AccessLogType.Open,
            emailId: email.Id,
            emailSubject: Truncate(email.Subject, 255),
            emailFrom: Truncate(email.From, 255));

        return Ok(EmailDetailDto.FromEntity(email));
    }

    /// <summary>
    /// Imports a raw RFC 822 message into the archive of one account. The message
    /// is only stored locally — nothing is written to the IMAP server.
    /// </summary>
    [HttpPost("")]
    public async Task<ActionResult<ImportEmailResultDto>> ImportMessage([FromBody] ImportEmailRequest request)
    {
        if (RequireWriteAccess() is { } writeError) return writeError;

        var errors = new Dictionary<string, string[]>();

        if (request.AccountId <= 0)
        {
            errors["accountId"] = new[] { "A valid accountId is required." };
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            errors["content"] = new[] { "The raw message content is required." };
        }

        var encoding = (request.Encoding ?? "base64").Trim().ToLowerInvariant();
        if (encoding is not ("base64" or "utf8"))
        {
            errors["encoding"] = new[] { "encoding must be 'base64' or 'utf8'." };
        }

        var folder = string.IsNullOrWhiteSpace(request.Folder) ? "INBOX" : request.Folder.Trim();
        if (folder.Length > MaxFolderNameLength || folder.Any(char.IsControl))
        {
            errors["folder"] = new[] { $"folder must be 1-{MaxFolderNameLength} characters without control characters." };
        }

        if (errors.Count > 0)
        {
            return ValidationProblem(new ValidationProblemDetails(errors));
        }

        var allowed = await GetAllowedAccountIdsAsync();
        if (allowed != null && !allowed.Contains(request.AccountId))
        {
            return NotFound();
        }

        var account = await _context.MailAccounts.FirstOrDefaultAsync(a => a.Id == request.AccountId);
        if (account == null)
        {
            return NotFound();
        }

        if (!ImportPayloadDecoder.TryDecode(request.Content, request.Encoding, out var rawMessage, out var decodeError))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid content encoding.",
                detail: decodeError);
        }

        if (rawMessage.Length > _options.MaxImportBytes)
        {
            return Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "Message too large.",
                detail: $"The decoded message exceeds the configured limit of {_options.MaxImportBytes} bytes (Api:MaxImportBytes).");
        }

        MimeMessage message;
        try
        {
            message = MimeMessage.Load(new MemoryStream(rawMessage));
        }
        catch (Exception ex)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid message.",
                detail: $"The content could not be parsed as an RFC 822 message: {ex.Message}");
        }

        var result = await _mailImporter.ImportEmailToDatabase(message, account, "api", folder);

        if (result.AlreadyExists)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Message already archived.",
                detail: "An identical message already exists in this account.",
                extensions: new Dictionary<string, object?>
                {
                    ["existingId"] = result.EmailId
                });
        }

        if (!result.Success)
        {
            return Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Import failed.",
                detail: result.Error);
        }

        await _accessLogService.LogAccessAsync(
            CurrentUsername,
            AccessLogType.Account,
            emailId: result.EmailId,
            emailSubject: Truncate(message.Subject, 255),
            emailFrom: Truncate(string.Join(", ", message.From.Mailboxes.Select(m => m.Address)), 255),
            mailAccountId: account.Id);

        var response = new ImportEmailResultDto
        {
            Id = result.EmailId ?? 0,
            AccountId = account.Id,
            Folder = folder,
            MessageId = message.MessageId ?? string.Empty,
            Subject = message.Subject ?? string.Empty
        };

        return CreatedAtAction(nameof(GetMessage), new { id = response.Id }, response);
    }

    /// <summary>
    /// Moves an archived message to another folder inside the archive. Message
    /// content is immutable (compliance), which is why the folder is the only
    /// mutable field.
    /// </summary>
    [HttpPatch("{id:int}")]
    public async Task<ActionResult<EmailSummaryDto>> MoveMessage(int id, [FromBody] MoveEmailRequest request)
    {
        if (RequireWriteAccess() is { } writeError) return writeError;

        var folderName = request.FolderName?.Trim();
        if (string.IsNullOrEmpty(folderName))
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["folderName"] = new[] { "folderName is required." }
            }));
        }

        if (folderName.Length > MaxFolderNameLength || folderName.Any(char.IsControl))
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["folderName"] = new[] { $"folderName must be 1-{MaxFolderNameLength} characters without control characters." }
            }));
        }

        var allowed = await GetAllowedAccountIdsAsync();
        var email = await _context.ArchivedEmails.FirstOrDefaultAsync(e => e.Id == id);
        if (email == null || (allowed != null && !allowed.Contains(email.MailAccountId)))
        {
            return NotFound();
        }

        var previousFolder = email.FolderName;
        email.FolderName = folderName;
        await _context.SaveChangesAsync();

        await _accessLogService.LogAccessAsync(
            CurrentUsername,
            AccessLogType.Account,
            emailId: email.Id,
            emailSubject: Truncate(email.Subject, 255),
            emailFrom: Truncate(email.From, 255),
            searchParameters: Truncate($"Moved via API: '{previousFolder}' -> '{folderName}'", 255),
            mailAccountId: email.MailAccountId);

        return Ok(EmailSummaryDto.FromEntity(email));
    }

    /// <summary>
    /// Deletes one archived message. Messages locked by the retention policy
    /// (compliance) cannot be deleted and answer 409.
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteMessage(int id)
    {
        if (RequireWriteAccess() is { } writeError) return writeError;
        if (RequireDeletionAllowed() is { } policyError) return policyError;

        if (!_options.AllowEmailDeletion)
        {
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "E-mail deletion is disabled.",
                detail: "Deleting archived messages through the API is disabled. Set Api:AllowEmailDeletion=true to enable it.");
        }

        var allowed = await GetAllowedAccountIdsAsync();
        var email = await _context.ArchivedEmails.FirstOrDefaultAsync(e => e.Id == id);
        if (email == null || (allowed != null && !allowed.Contains(email.MailAccountId)))
        {
            return NotFound();
        }

        if (email.IsLocked)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Message is locked.",
                detail: "This message is locked by the retention policy (compliance) and cannot be deleted.");
        }

        var subject = email.Subject;
        var from = email.From;
        var accountId = email.MailAccountId;

        _context.ArchivedEmails.Remove(email);
        await _context.SaveChangesAsync();

        await _accessLogService.LogAccessAsync(
            CurrentUsername,
            AccessLogType.Deletion,
            emailId: id,
            emailSubject: Truncate(subject, 255),
            emailFrom: Truncate(from, 255),
            mailAccountId: accountId);

        return NoContent();
    }

    [HttpGet("{id:int}/attachments/{attachmentId:int}")]
    public async Task<IActionResult> DownloadAttachment(int id, int attachmentId)
    {
        if (!_options.AllowAttachmentDownloads)
        {
            return Problem(statusCode: 403, title: "Attachment downloads are disabled.");
        }

        var allowed = await GetAllowedAccountIdsAsync();
        var att = await _context.EmailAttachments
            .Include(a => a.AttachmentContent)
            .Include(a => a.ArchivedEmail)
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.ArchivedEmailId == id);

        if (att == null || (allowed != null && !allowed.Contains(att.ArchivedEmail.MailAccountId)))
        {
            return NotFound();
        }

        await _accessLogService.LogAccessAsync(CurrentUsername, AccessLogType.Download, emailId: id);

        return File(att.Content, att.ContentType, att.FileName);
    }

    private static DirectionParseResult ParseDirection(string? direction)
    {
        return direction?.Trim().ToLowerInvariant() switch
        {
            null or "" => DirectionParseResult.Unspecified,
            "incoming" => DirectionParseResult.Incoming,
            "outgoing" => DirectionParseResult.Outgoing,
            _ => DirectionParseResult.Invalid
        };
    }

    private static string BuildSearchSummary(
        string? q,
        DateTime? from,
        DateTime? to,
        int? accountId,
        string? folder,
        string? direction,
        int page,
        int pageSize,
        string? sortBy,
        string? sortOrder)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(q)) parts.Add($"q={q}");
        if (from.HasValue) parts.Add($"from={from:O}");
        if (to.HasValue) parts.Add($"to={to:O}");
        if (accountId.HasValue) parts.Add($"accountId={accountId}");
        if (!string.IsNullOrWhiteSpace(folder)) parts.Add($"folder={folder}");
        if (!string.IsNullOrWhiteSpace(direction)) parts.Add($"direction={direction}");
        parts.Add($"page={page}");
        parts.Add($"pageSize={pageSize}");
        parts.Add($"sortBy={sortBy}");
        parts.Add($"sortOrder={sortOrder}");

        return Truncate(string.Join("; ", parts), 255);
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value ?? string.Empty;
        }

        return value[..maxLength];
    }

    private enum DirectionParseResult
    {
        Invalid,
        Unspecified,
        Incoming,
        Outgoing
    }
}

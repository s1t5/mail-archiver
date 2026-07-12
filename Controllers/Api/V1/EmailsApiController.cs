using MailArchiver.Controllers.Api;
using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Models.Api;
using MailArchiver.Services;
using MailArchiver.Services.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MailArchiver.Controllers.Api.V1;

[Route("api/v1/emails")]
public class EmailsApiController : ApiControllerBase
{
    private readonly MailArchiverDbContext _context;
    private readonly EmailCoreService _emailCoreService;
    private readonly ApiOptions _options;
    private readonly IAccessLogService _accessLogService;
    private readonly IAuthenticationService _authService;

    public EmailsApiController(
        MailArchiverDbContext context,
        EmailCoreService emailCoreService,
        IOptions<ApiOptions> options,
        IAccessLogService accessLogService,
        IAuthenticationService authService)
    {
        _context = context;
        _emailCoreService = emailCoreService;
        _options = options.Value;
        _accessLogService = accessLogService;
        _authService = authService;
    }

    private string CurrentUsername => _authService.GetCurrentUserDisplayName(HttpContext);

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

        var isOutgoing = ParseDirection(direction);
        if (isOutgoing == DirectionParseResult.Invalid)
        {
            return BadRequest();
        }

        if (!TryGetSortBy(sortBy, out var canonicalSortBy))
        {
            return BadRequest();
        }

        if (!TryGetSortOrder(sortOrder, out var canonicalSortOrder))
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
            canonicalSortBy,
            canonicalSortOrder);

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
            searchParameters: BuildSearchSummary(q, from, to, accountId, folder, direction, page, pageSize, canonicalSortBy, canonicalSortOrder));

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

    private static bool TryGetSortBy(string? sortBy, out string canonicalSortBy)
    {
        canonicalSortBy = sortBy?.Trim().ToLowerInvariant() switch
        {
            null or "" or "sentdate" => "SentDate",
            "receiveddate" => "ReceivedDate",
            "subject" => "Subject",
            "from" => "From",
            "to" => "To",
            _ => string.Empty
        };

        return canonicalSortBy.Length > 0;
    }

    private static bool TryGetSortOrder(string? sortOrder, out string canonicalSortOrder)
    {
        canonicalSortOrder = sortOrder?.Trim().ToLowerInvariant() switch
        {
            null or "" => "desc",
            "asc" => "asc",
            "desc" => "desc",
            _ => string.Empty
        };

        return canonicalSortOrder.Length > 0;
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
        string sortBy,
        string sortOrder)
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

using MailArchiver.Attributes;
using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services;
using MailArchiver.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace MailArchiver.Controllers
{
    [UserAccessRequired]
    public class LogsController : Controller
    {
        private readonly IAccessLogService _accessLogService;
        private readonly IAuthenticationService _authenticationService;
        private readonly MailArchiverDbContext _context;
        private readonly IAuditExportService _auditExportService;
        private readonly IOptions<AuditExportOptions> _auditExportOptions;
        private readonly Microsoft.Extensions.Localization.IStringLocalizer<MailArchiver.SharedResource> _localizer;

        public LogsController(IAccessLogService accessLogService, IAuthenticationService authenticationService, MailArchiverDbContext context, IAuditExportService auditExportService, IOptions<AuditExportOptions> auditExportOptions, Microsoft.Extensions.Localization.IStringLocalizer<MailArchiver.SharedResource> localizer)
        {
            _accessLogService = accessLogService;
            _authenticationService = authenticationService;
            _context = context;
            _auditExportService = auditExportService;
            _auditExportOptions = auditExportOptions;
            _localizer = localizer;
        }

        public async Task<IActionResult> Index(int page = 1, int pageSize = 50, DateTime? fromDate = null, DateTime? toDate = null, string username = null, AccessLogType? type = null)
        {
            var currentUsername = _authenticationService.GetCurrentUserDisplayName(HttpContext);
            var isAdmin = _authenticationService.IsCurrentUserAdmin(HttpContext);

            // Set default page size to 50
            pageSize = 50;

            // Get logs based on user role with date filtering
            List<AccessLog> logs;
            if (isAdmin)
            {
                // For admin users, check if a specific username was requested for filtering
                if (!string.IsNullOrEmpty(username))
                {
                    logs = await _accessLogService.GetLogsForUserAsync(username, fromDate, toDate);
                }
                else
                {
                    logs = await _accessLogService.GetLogsForAdminAsync(fromDate, toDate); // Get all logs for admin
                }
            }
            else
            {
                // For non-admin users, they can only see their own logs regardless of the username parameter
                logs = await _accessLogService.GetLogsForUserAsync(currentUsername, fromDate, toDate); // Get only user's logs
            }

            // Filter by type if specified
            if (type.HasValue)
            {
                logs = logs.Where(l => l.Type == type.Value).ToList();
            }

            // Order by timestamp descending (newest first) - already done in service

            // Implement pagination
            var totalLogs = logs.Count;
            var totalPages = (int)Math.Ceiling((double)totalLogs / pageSize);
            var paginatedLogs = logs.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            // For admin users, get all usernames for the filter dropdown
            if (isAdmin)
            {
                var allUsers = await _context.Users
                    .OrderBy(u => u.Username)
                    .Select(u => u.Username)
                    .ToListAsync();
                ViewBag.AllUsers = allUsers;
            }

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.PageSize = pageSize;
            ViewBag.IsAdmin = isAdmin;
            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;
            ViewBag.UsernameFilter = username;
            ViewBag.TypeFilter = type;

            return View(paginatedLogs);
        }

        // GET: Logs/AuditExport
        [HttpGet]
        public async Task<IActionResult> AuditExport()
        {
            if (!_authenticationService.IsCurrentUserAdmin(HttpContext))
            {
                return RedirectToAction("AccessDenied", "Auth", null);
            }

            var mailAccounts = await _context.MailAccounts
                .OrderBy(a => a.Name)
                .Select(a => new AuditExportMailAccountViewModel
                {
                    Id = a.Id,
                    Name = a.Name,
                    EmailAddress = a.EmailAddress
                })
                .ToListAsync();

            var jobs = await _auditExportService.GetRecentJobsAsync(20);

            ViewBag.AuditExportMailAccounts = mailAccounts;
            ViewBag.AuditExportOptions = _auditExportOptions.Value;
            return View(jobs);
        }

        // POST: Logs/StartAuditExport
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartAuditExport(DateTime fromDate, DateTime toDate, int? mailAccountId, bool includeAttachments = false, string? dataSupplierName = null, string? dataSupplierLocation = null, string? dataSupplierComment = null)
        {
            if (!_authenticationService.IsCurrentUserAdmin(HttpContext))
            {
                return RedirectToAction("AccessDenied", "Auth", null);
            }

            var username = _authenticationService.GetCurrentUserDisplayName(HttpContext) ?? "unknown";

            // Validate period
            if (fromDate == default || toDate == default)
            {
                TempData["ErrorMessage"] = _localizer["AuditExportInvalidPeriod"].Value;
                return RedirectToAction(nameof(AuditExport));
            }

            if (fromDate > toDate)
            {
                (fromDate, toDate) = (toDate, fromDate);
            }

            var maxRangeYears = Math.Max(1, _auditExportOptions.Value.MaxRangeYears);
            if (toDate > fromDate.AddYears(maxRangeYears))
            {
                TempData["ErrorMessage"] = _localizer["AuditExportRangeTooLong", maxRangeYears].Value;
                return RedirectToAction(nameof(AuditExport));
            }

            // Validate mailbox if one was selected
            if (mailAccountId.HasValue)
            {
                var accountExists = await _context.MailAccounts.AnyAsync(a => a.Id == mailAccountId.Value);
                if (!accountExists)
                {
                    TempData["ErrorMessage"] = _localizer["AuditExportAccountNotFound"].Value;
                    return RedirectToAction(nameof(AuditExport));
                }
            }

            // Email dates are stored in the configured display timezone, exactly like the
            // archive search filters, so the naive date inputs are compared directly.
            var request = new AuditExportRequest
            {
                FromDate = DateTime.SpecifyKind(fromDate, DateTimeKind.Unspecified),
                ToDate = DateTime.SpecifyKind(toDate, DateTimeKind.Unspecified).AddHours(23).AddMinutes(59).AddSeconds(59),
                MailAccountId = mailAccountId,
                IncludeAttachments = includeAttachments,
                DataSupplierName = dataSupplierName?.Trim(),
                DataSupplierLocation = dataSupplierLocation?.Trim(),
                DataSupplierComment = dataSupplierComment?.Trim()
            };

            var job = await _auditExportService.StartJobAsync(request, username);

            // Revision-safe start entry; completion is logged by the service itself
            var parameters = JsonSerializer.Serialize(new
            {
                jobId = job.Id,
                period = $"{request.FromDate:yyyy-MM-dd} - {request.ToDate:yyyy-MM-dd}",
                mailbox = mailAccountId.HasValue ? (job.MailAccountName ?? mailAccountId.ToString()) : "*",
                includeAttachments
            });
            await _accessLogService.LogAccessAsync(username, AccessLogType.AuditExport, searchParameters: parameters, mailAccountId: mailAccountId);

            return RedirectToAction(nameof(AuditExport));
        }

        // GET: Logs/AuditExportStatus
        [HttpGet]
        public async Task<IActionResult> AuditExportStatus(Guid jobId)
        {
            if (!_authenticationService.IsCurrentUserAdmin(HttpContext))
            {
                return Forbid();
            }

            var job = await _auditExportService.GetJobAsync(jobId);
            if (job == null)
            {
                return NotFound();
            }

            return Json(new
            {
                jobId = job.Id,
                status = job.Status.ToString(),
                created = job.Created,
                started = job.Started,
                completed = job.Completed,
                totalEmails = job.TotalEmails,
                processedEmails = job.ProcessedEmails,
                outputFileSize = job.OutputFileSize,
                errorMessage = job.ErrorMessage
            });
        }

        // GET: Logs/DownloadAuditExport
        [HttpGet]
        public async Task<IActionResult> DownloadAuditExport(Guid jobId)
        {
            if (!_authenticationService.IsCurrentUserAdmin(HttpContext))
            {
                return Forbid();
            }

            var fileResult = await _auditExportService.GetExportForDownloadAsync(jobId);
            if (fileResult == null)
            {
                TempData["ErrorMessage"] = _localizer["AuditExportFileNotFound"].Value;
                return RedirectToAction(nameof(AuditExport));
            }

            var fileStream = new FileStream(fileResult.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            await _auditExportService.MarkAsDownloadedAsync(jobId);
            return File(fileStream, fileResult.ContentType, fileResult.FileName);
        }

        // POST: Logs/CancelAuditExport
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelAuditExport(Guid jobId)
        {
            if (!_authenticationService.IsCurrentUserAdmin(HttpContext))
            {
                return RedirectToAction("AccessDenied", "Auth", null);
            }

            await _auditExportService.CancelJobAsync(jobId);
            return RedirectToAction(nameof(AuditExport));
        }
    }
}
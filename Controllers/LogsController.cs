using MailArchiver.Attributes;
using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MailArchiver.Controllers
{
    [UserAccessRequired]
    public class LogsController : Controller
    {
        private readonly IAccessLogService _accessLogService;
        private readonly IAuthenticationService _authenticationService;
        private readonly MailArchiverDbContext _context;
        private readonly IEmailService _emailService;

        public LogsController(IAccessLogService accessLogService, IAuthenticationService authenticationService, MailArchiverDbContext context, IEmailService emailService)
        {
            _accessLogService = accessLogService;
            _authenticationService = authenticationService;
            _context = context;
            _emailService = emailService;
        }

        public async Task<IActionResult> Index(int page = 1, int pageSize = 50, DateTime? fromDate = null, DateTime? toDate = null, string username = null)
        {
            var currentUsername = _authenticationService.GetCurrentUser(HttpContext);
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

            return View(paginatedLogs);
        }
    }
}

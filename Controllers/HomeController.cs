using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Models.ViewModels;
using MailArchiver.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Globalization;

namespace MailArchiver.Controllers
{
    public class HomeController : Controller
    {
        private readonly IEmailService _emailService;
        private readonly IUserService _userService;
        private readonly MailArchiverDbContext _context;
        private readonly ILogger<HomeController> _logger;
        private readonly IBatchRestoreService? _batchRestoreService;
        private readonly IAuthenticationService _authenticationService;

        public HomeController(
            IEmailService emailService, 
            IUserService userService,
            MailArchiverDbContext context,
            IAuthenticationService authenticationService,
            ILogger<HomeController> logger, 
            IBatchRestoreService? batchRestoreService = null)
        {
            _emailService = emailService;
            _userService = userService;
            _context = context;
            _authenticationService = authenticationService;
            _logger = logger;
            _batchRestoreService = batchRestoreService;
        }

        public async Task<IActionResult> Index()
        {
            // Get current user
            var currentUsername = _authenticationService.GetCurrentUser(HttpContext);
            var currentUser = await _userService.GetUserByUsernameAsync(currentUsername);
            
            DashboardViewModel model;
            
            // If user is admin, show all accounts, otherwise show only assigned accounts
            if (currentUser != null && currentUser.IsAdmin)
            {
                model = await _emailService.GetDashboardStatisticsAsync();
            }
            else if (currentUser != null)
            {
                // Get only accounts assigned to this user
                var userAccounts = await _userService.GetUserMailAccountsAsync(currentUser.Id);
                var accountIds = userAccounts.Select(a => a.Id).ToList();
                
                // Create a custom dashboard model for this user
                model = await CreateCustomDashboardStatisticsAsync(accountIds);
            }
            else
            {
                // Fallback to default dashboard
                model = await _emailService.GetDashboardStatisticsAsync();
            }

            // Aktive Jobs für Dashboard anzeigen
            if (_batchRestoreService != null)
            {
                var activeJobs = _batchRestoreService.GetActiveJobs();
                ViewBag.ActiveJobsCount = activeJobs.Count;
            }

            return View(model);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
        
        private async Task<DashboardViewModel> CreateCustomDashboardStatisticsAsync(List<int> accountIds)
        {
            var model = new DashboardViewModel();

            model.TotalEmails = await _context.ArchivedEmails
                .CountAsync(e => accountIds.Contains(e.MailAccountId));
            model.TotalAccounts = accountIds.Count;
            model.TotalAttachments = await _context.EmailAttachments
                .Where(a => _context.ArchivedEmails
                    .Where(e => accountIds.Contains(e.MailAccountId))
                    .Select(e => e.Id)
                    .Contains(a.ArchivedEmailId))
                .CountAsync();

            var totalSizeBytes = await _context.EmailAttachments
                .Where(a => _context.ArchivedEmails
                    .Where(e => accountIds.Contains(e.MailAccountId))
                    .Select(e => e.Id)
                    .Contains(a.ArchivedEmailId))
                .SumAsync(a => (long)a.Size);
            model.TotalStorageUsed = FormatFileSize(totalSizeBytes);

            model.EmailsPerAccount = await _context.MailAccounts
                .Where(a => accountIds.Contains(a.Id))
                .Select(a => new AccountStatistics
                {
                    AccountName = a.Name,
                    EmailAddress = a.EmailAddress,
                    EmailCount = a.ArchivedEmails.Count(e => accountIds.Contains(e.MailAccountId)),
                    LastSyncTime = a.LastSync,
                    IsEnabled = a.IsEnabled
                })
                .ToListAsync();

            var startDate = DateTime.UtcNow.AddMonths(-11).Date;
            var months = new List<EmailCountByPeriod>();
            for (int i = 0; i < 12; i++)
            {
                var currentMonth = startDate.AddMonths(i);
                var nextMonth = currentMonth.AddMonths(1);

                var count = await _context.ArchivedEmails
                    .Where(e => accountIds.Contains(e.MailAccountId) && 
                        e.SentDate >= currentMonth && e.SentDate < nextMonth)
                    .CountAsync();

                months.Add(new EmailCountByPeriod
                {
                    Period = $"{CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(currentMonth.Month)} {currentMonth.Year}",
                    Count = count
                });
            }
            model.EmailsByMonth = months;

            model.TopSenders = await _context.ArchivedEmails
                .Where(e => !e.IsOutgoing && accountIds.Contains(e.MailAccountId))
                .GroupBy(e => e.From)
                .Select(g => new EmailCountByAddress
                {
                    EmailAddress = g.Key,
                    Count = g.Count()
                })
                .OrderByDescending(e => e.Count)
                .Take(10)
                .ToListAsync();

            model.RecentEmails = await _context.ArchivedEmails
                .Include(e => e.MailAccount)
                .Where(e => accountIds.Contains(e.MailAccountId))
                .OrderByDescending(e => e.ReceivedDate)
                .Take(10)
                .ToListAsync();

            return model;
        }
        
        private string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            return $"{number:n1} {suffixes[counter]}";
        }
    }
}

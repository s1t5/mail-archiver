using MailArchiver.Models;
using MailArchiver.Models.ViewModels;
using MailArchiver.Services;
using MailArchiver.Services.Shared;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace MailArchiver.Controllers
{
    public class HomeController : Controller
    {
        private readonly MailArchiver.Services.Core.EmailCoreService _emailCoreService;
        private readonly IUserService _userService;
        private readonly MailArchiver.Services.IAuthenticationService _authenticationService;
        private readonly IVersionUpdateService _versionUpdateService;
        private readonly IAccountStorageService _accountStorageService;
        private readonly ISyncJobService _syncJobService;

        public HomeController(
            MailArchiver.Services.Core.EmailCoreService emailCoreService, 
            IUserService userService,
            MailArchiver.Services.IAuthenticationService authenticationService,
            IVersionUpdateService versionUpdateService,
            IAccountStorageService accountStorageService,
            ISyncJobService syncJobService,
            IBatchRestoreService? batchRestoreService = null)
        {
            _emailCoreService = emailCoreService;
            _userService = userService;
            _authenticationService = authenticationService;
            _versionUpdateService = versionUpdateService;
            _accountStorageService = accountStorageService;
            _syncJobService = syncJobService;
            _batchRestoreService = batchRestoreService;
        }

        private readonly IBatchRestoreService? _batchRestoreService;

        public async Task<IActionResult> Index()
        {
            // Get current user
            var currentUsername = _authenticationService.GetCurrentUserDisplayName(HttpContext);
            var currentUser = await _userService.GetUserByUsernameAsync(currentUsername);
            
            DashboardViewModel model;
            
            // If user is admin, show all accounts, otherwise show only assigned accounts
            if (currentUser != null && currentUser.IsAdmin)
            {
                model = await _emailCoreService.GetDashboardStatisticsAsync();
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
                model = await _emailCoreService.GetDashboardStatisticsAsync();
            }

            // Speicherverbrauch pro Account befuellen (aus Cache)
            if (model.EmailsPerAccount != null && model.EmailsPerAccount.Count > 0)
            {
                var accountIds = model.EmailsPerAccount.Select(a => a.AccountId).ToList();
                var storageMap = await _accountStorageService.GetStorageForAccountsAsync(accountIds);
                foreach (var stat in model.EmailsPerAccount)
                {
                    stat.StorageUsed = storageMap.TryGetValue(stat.AccountId, out var storage)
                        ? storage
                        : AccountStorageService.FormatFileSize(0);

                    var isSyncing = _syncJobService.IsAccountSyncing(stat.AccountId);
                    stat.IsSyncing = isSyncing;
                    stat.IsSyncPending = !isSyncing
                        && stat.Provider != ProviderType.IMPORT
                        && stat.LastSyncTime <= new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                }
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

        /// <summary>
        /// Returns release notes as rendered HTML for the current app version.
        /// Only accessible by admin users.
        /// </summary>
        [HttpGet]
        [MailArchiver.Attributes.AdminRequired]
        [MailArchiver.Attributes.EmailAccessRequired]
        public async Task<IActionResult> GetReleaseNotes()
        {
            var currentUsername = _authenticationService.GetCurrentUserDisplayName(HttpContext);
            var currentUser = await _userService.GetUserByUsernameAsync(currentUsername);
            if (currentUser == null)
                return Unauthorized();

            var result = await _versionUpdateService.GetReleaseNotesForCurrentVersionAsync(currentUser.Id);

            if (!result.ShouldShow || string.IsNullOrWhiteSpace(result.Body))
                return Json(new { show = false });

            // Render Markdown to HTML using the built-in converter (no external dependency)
            var html = MarkdownHelper.ToHtml(result.Body);

            return Json(new
            {
                show = true,
                version = result.Version,
                bodyHtml = html
            });
        }

        /// <summary>
        /// Dismisses the current version changelog for the admin user.
        /// </summary>
        [HttpPost]
        [MailArchiver.Attributes.AdminRequired]
        [MailArchiver.Attributes.EmailAccessRequired]
        public async Task<IActionResult> DismissVersion()
        {
            var currentUsername = _authenticationService.GetCurrentUserDisplayName(HttpContext);
            var currentUser = await _userService.GetUserByUsernameAsync(currentUsername);
            if (currentUser == null)
                return Unauthorized();

            await _versionUpdateService.DismissVersionAsync(currentUser.Id);
            return Ok();
        }

        private async Task<DashboardViewModel> CreateCustomDashboardStatisticsAsync(List<int> accountIds)
        {
            // Cache per unique account assignment so repeated dashboard loads by the
            // same user (or users sharing the same accounts) hit the memory cache.
            var cacheKeySuffix = "user-" + string.Join(",", accountIds.OrderBy(id => id));
            return await _emailCoreService.GetOrCreateCachedStatisticsAsync(cacheKeySuffix, ctx =>
            {
                var model = new DashboardViewModel();

                var accountEmails = ctx.ArchivedEmails
                    .Where(e => accountIds.Contains(e.MailAccountId));

                model.TotalEmails = accountEmails.Count();
                model.TotalAccounts = accountIds.Count;
                model.TotalAttachments = ctx.EmailAttachments
                    .Count(a => accountEmails.Any(e => e.Id == a.ArchivedEmailId));

                model.EmailsPerAccount = ctx.MailAccounts
                    .Where(a => accountIds.Contains(a.Id))
                    .Select(a => new AccountStatistics
                    {
                        AccountId = a.Id,
                        AccountName = a.Name,
                        EmailAddress = a.EmailAddress,
                        EmailCount = a.ArchivedEmails.Count,
                        LastSyncTime = a.LastSync,
                        IsEnabled = a.IsEnabled,
                        Provider = a.Provider
                    })
                    .ToList();

                model.EmailsByMonth = MailArchiver.Services.Core.EmailCoreService
                    .BuildEmailsByMonth(accountEmails);

                model.TopSenders = accountEmails
                    .Where(e => !e.IsOutgoing)
                    .GroupBy(e => e.From)
                    .Select(g => new EmailCountByAddress
                    {
                        EmailAddress = g.Key,
                        Count = g.Count()
                    })
                    .OrderByDescending(e => e.Count)
                    .Take(10)
                    .ToList();

                model.RecentEmails = accountEmails
                    .OrderByDescending(e => e.SentDate)
                    .Select(e => new RecentEmailDto
                    {
                        Id = e.Id,
                        Subject = e.Subject,
                        From = e.From,
                        SentDate = e.SentDate,
                        IsOutgoing = e.IsOutgoing,
                        MailAccountName = e.MailAccount.Name
                    })
                    .Take(10)
                    .ToList();

                return model;
            });
        }
    }
}
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
            var accountScope = await ResolveDashboardScopeAsync();

            // Every account, or only the ones assigned to this user
            DashboardViewModel model = accountScope == null
                ? await _emailCoreService.GetDashboardStatisticsAsync(LastRunHadIssues)
                : await CreateCustomDashboardStatisticsAsync(accountScope);

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

                    stat.LastRunHadIssues = LastRunHadIssues(stat.AccountId);
                }

                // The panel order was computed when the statistics were cached, the markers above
                // were just read fresh. Re-applying the same rule to the decorated rows keeps the
                // two from drifting apart within the cache window.
                MailArchiver.Services.Core.EmailCoreService.ApplyPanelOrder(
                    model.EmailsPerAccount, LastRunHadIssues);
            }

            // Aktive Jobs für Dashboard anzeigen
            if (_batchRestoreService != null)
            {
                var activeJobs = _batchRestoreService.GetActiveJobs();
                ViewBag.ActiveJobsCount = activeJobs.Count;
            }

            model.ShowDirectionSplits = _emailCoreService.ShowDirectionSplits;
            model.SelectablePeriods = _emailCoreService.SelectablePeriods;

            return View(model);
        }

        /// <summary>
        /// The accounts this request may see on the dashboard: null for all of them, a list of
        /// ids otherwise. The page and the chart endpoint both take the scope from here, so a
        /// chart can never answer for accounts the page would not show.
        /// </summary>
        private async Task<List<int>?> ResolveDashboardScopeAsync()
        {
            var currentUsername = _authenticationService.GetCurrentUserDisplayName(HttpContext);
            var currentUser = await _userService.GetUserByUsernameAsync(currentUsername);

            if (currentUser == null || currentUser.IsAdmin)
                return null;

            var userAccounts = await _userService.GetUserMailAccountsAsync(currentUser.Id);
            return userAccounts.Select(a => a.Id).ToList();
        }

        /// <summary>
        /// The dashboard charts for a chosen bucket width, window, sender direction and position
        /// in the past. Answers with the selection it actually used, which is not always the one
        /// that was asked for: not every width and window make a chart worth drawing, and a
        /// position past the oldest mail is pulled back to the furthest one that holds any. The
        /// browser follows what comes back rather than what it sent, and takes the state of its
        /// paging buttons from the answer instead of working it out again.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ChartData(
            string? granularity, string? window, string? senders, int offset = 0)
        {
            var accountScope = await ResolveDashboardScopeAsync();
            var (resolvedGranularity, resolvedWindow) = DashboardPeriods.Canonicalize(granularity, window);
            var outgoingSenders = string.Equals(senders, "outgoing", StringComparison.OrdinalIgnoreCase);

            var series = await _emailCoreService.GetChartSeriesAsync(
                accountScope, resolvedGranularity, resolvedWindow, outgoingSenders, offset);

            // Switched off, this route behaves as if it did not exist, the way the keyed API
            // routes do: a hand made request must not be able to drive the queries behind a
            // feature that is not on.
            if (series == null)
                return NotFound();

            return Json(new
            {
                granularity = series.Granularity,
                window = series.Window,
                senders = series.OutgoingSenders ? "outgoing" : "incoming",
                offset = series.Offset,
                canGoBack = series.CanGoBack,
                canGoForward = series.CanGoForward,
                rangeLabel = series.RangeLabel,
                labels = series.Emails.Select(e => e.Period),
                incoming = series.Emails.Select(e => e.Incoming),
                outgoing = series.Emails.Select(e => e.Outgoing),
                senderLabels = series.TopSenders.Select(s => s.EmailAddress),
                senderCounts = series.TopSenders.Select(s => s.Count)
            });
        }

        /// <summary>
        /// Whether an account's last finished run reported anything. One definition for both uses:
        /// it decides the warning marker on a row and which rows the panel keeps at all, and those
        /// two drifting apart would show a panel ordered by one rule and marked by another.
        /// </summary>
        private bool LastRunHadIssues(int accountId)
        {
            var lastRun = _syncJobService.GetLastCompletedJobForAccount(accountId);
            return lastRun != null
                && ((!lastRun.FailuresAcknowledged && (lastRun.FailedEmails > 0 || lastRun.FailedFolders > 0))
                    || lastRun.MissingFolders > 0);
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

                var accountAttachments = ctx.EmailAttachments
                    .Where(a => accountIds.Contains(a.ArchivedEmail.MailAccountId));

                if (_emailCoreService.ShowDirectionSplits)
                {
                    var emails = MailArchiver.Services.Core.EmailCoreService
                        .CountEmailsByDirection(accountEmails);
                    var attachments = MailArchiver.Services.Core.EmailCoreService
                        .CountAttachmentsByDirection(accountAttachments);

                    // The addresses come from the account rows, so the account number is taken
                    // from the same read instead of from the assignment list: an assignment that
                    // outlived its account would otherwise be counted as an account without a
                    // domain.
                    var accountAddresses = ctx.MailAccounts
                        .Where(a => accountIds.Contains(a.Id))
                        .Select(a => a.EmailAddress)
                        .ToList();

                    model.TotalEmails = emails.Total;
                    model.IncomingEmails = emails.Incoming;
                    model.OutgoingEmails = emails.Outgoing;
                    model.TotalAccounts = accountAddresses.Count;
                    model.AccountDomains = MailArchiver.Services.Core.EmailCoreService
                        .CountAccountDomains(accountAddresses);
                    model.TotalAttachments = attachments.Total;
                    model.IncomingAttachments = attachments.Incoming;
                    model.OutgoingAttachments = attachments.Outgoing;
                }
                else
                {
                    model.TotalEmails = accountEmails.Count();
                    model.TotalAccounts = ctx.MailAccounts.Count(a => accountIds.Contains(a.Id));
                    model.TotalAttachments = accountAttachments.Count();
                }

                // Same rule as the admin dashboard, and through the same helper: a self-manager
                // watching a handful of accounts has the same reason to see a troubled one first.
                model.EmailsPerAccount = MailArchiver.Services.Core.EmailCoreService.BuildAccountPanel(
                    ctx.MailAccounts.Where(a => accountIds.Contains(a.Id)),
                    LastRunHadIssues);

                model.Series = _emailCoreService.BuildDefaultSeries(accountEmails);

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
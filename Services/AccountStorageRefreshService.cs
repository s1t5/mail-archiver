using MailArchiver.Data;
using MailArchiver.Models;
using Microsoft.EntityFrameworkCore;

namespace MailArchiver.Services
{
    /// <summary>
    /// Autarker Background-Service fuer den Speicherverbrauch-Cache.
    /// Unabhaengig von DatabaseMaintenance:Enabled.
    /// 1. Beim Start: Resumable Backfill aller Pending Accounts (crash-safe).
    /// 2. Danach: Taeglicher Full-Refresh.
    /// </summary>
    public class AccountStorageRefreshService : BackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<AccountStorageRefreshService> _logger;
        private readonly IConfiguration _configuration;

        public AccountStorageRefreshService(
            IServiceScopeFactory serviceScopeFactory,
            ILogger<AccountStorageRefreshService> logger,
            IConfiguration configuration)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var enabled = _configuration.GetValue<bool>("AccountStorage:Enabled", true);

            if (!enabled)
            {
                _logger.LogInformation("Account Storage Refresh Service is disabled in configuration");
                return;
            }

            _logger.LogInformation("Account Storage Refresh Service is starting...");

            var dailyExecutionTime = _configuration.GetValue<string>("AccountStorage:DailyExecutionTime", "02:30");
            if (!TimeSpan.TryParse(dailyExecutionTime, out TimeSpan executionTime))
            {
                _logger.LogError("Invalid AccountStorage:DailyExecutionTime format: {Time}. Expected HH:mm. Service will not run.", dailyExecutionTime);
                return;
            }

            // Phase 1: Resumable Backfill beim Start
            try
            {
                await RunBackfillAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during storage backfill: {Message}", ex.Message);
            }

            if (stoppingToken.IsCancellationRequested)
                return;

            // Phase 2: Taeglicher Full-Refresh
            _logger.LogInformation("Storage backfill complete. Switching to daily refresh schedule at {Time}", dailyExecutionTime);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var now = DateTime.Now;
                    var nextRun = CalculateNextRunTime(executionTime);
                    var delay = nextRun - now;

                    _logger.LogInformation("Next storage refresh scheduled for {NextRun} (in {Hours:F1} hours)",
                        nextRun, delay.TotalHours);

                    await Task.Delay(delay, stoppingToken);

                    if (stoppingToken.IsCancellationRequested)
                        break;

                    _logger.LogInformation("Starting scheduled storage refresh at {Time}", DateTime.Now);
                    using (var scope = _serviceScopeFactory.CreateScope())
                    {
                        var storageService = scope.ServiceProvider.GetRequiredService<IAccountStorageService>();
                        await storageService.RefreshAllAccountStorageAsync(stoppingToken);
                    }
                    _logger.LogInformation("Scheduled storage refresh completed at {Time}", DateTime.Now);

                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Account Storage Refresh Service is stopping (operation cancelled)");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in storage refresh loop: {Message}", ex.Message);
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
            }

            _logger.LogInformation("Account Storage Refresh Service has stopped");
        }

        private async Task RunBackfillAsync(CancellationToken stoppingToken)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var storageService = scope.ServiceProvider.GetRequiredService<IAccountStorageService>();
            var context = scope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();

            // State-Eintraege fuer alle Accounts sicherstellen (neue Accounts)
            await storageService.EnsureBackfillStatesAsync();

            var backfillDelayMs = _configuration.GetValue<int>("AccountStorage:BackfillDelayMs", 5000);

            // Alle Pending Accounts laden (resumable)
            var pendingAccounts = await context.AccountStorageBackfillStates
                .Where(s => s.Status == "Pending")
                .OrderBy(s => s.MailAccountId)
                .Select(s => s.MailAccountId)
                .ToListAsync(stoppingToken);

            if (pendingAccounts.Count == 0)
            {
                _logger.LogInformation("No pending storage backfill accounts found");
                return;
            }

            _logger.LogInformation("Starting storage backfill for {Count} accounts", pendingAccounts.Count);

            int processed = 0;
            foreach (var accountId in pendingAccounts)
            {
                if (stoppingToken.IsCancellationRequested)
                    break;

                try
                {
                    _logger.LogInformation("Backfill: calculating storage for account {AccountId} ({Processed}/{Total})",
                        accountId, processed + 1, pendingAccounts.Count);

                    await storageService.RefreshAccountStorageAsync(accountId);
                    processed++;

                    if (backfillDelayMs > 0)
                        await Task.Delay(backfillDelayMs, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during backfill for account {AccountId}: {Message}", accountId, ex.Message);
                    // Weiter mit naechstem Account (resumable)
                }
            }

            _logger.LogInformation("Storage backfill finished. Processed {Processed} of {Total} accounts", processed, pendingAccounts.Count);
        }

        private static DateTime CalculateNextRunTime(TimeSpan executionTime)
        {
            var now = DateTime.Now;
            var nextRun = now.Date.Add(executionTime);
            if (nextRun <= now)
                nextRun = nextRun.AddDays(1);
            return nextRun;
        }
    }
}

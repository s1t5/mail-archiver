using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MailArchiver.Services
{
    public class MailSyncBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MailSyncBackgroundService> _logger;
        private readonly IConfiguration _configuration;

        // Polling loop cadence. Short enough that per-account intervals (down to 1 minute)
        // are respected reasonably, long enough to avoid busy-waiting.
        private const int PollIntervalSeconds = 60;
        // Pause between two consecutive account syncs within one poll cycle.
        private const int InterAccountDelaySeconds = 10;
        // Sentinel watermark meaning "no sync yet, force a full sync".
        private static readonly DateTime EpochUtc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public MailSyncBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<MailSyncBackgroundService> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Mail Sync Background Service is starting...");

            // Global default interval (minutes) from appsettings. Per-account value
            // overrides this when set; null falls back to this default.
            var defaultSyncIntervalMinutes = _configuration.GetValue<int>("MailSync:IntervalMinutes", 15);
            // Global default full-sync interval (hours) from appsettings. Nullable on
            // purpose: null (the default) means "no automatic full sync" for accounts
            // that do not set their own FullSyncIntervalHours. Per-account value wins.
            var defaultFullSyncIntervalHours = _configuration.GetValue<int?>("MailSync:FullSyncIntervalHours");
            var syncTimeoutMinutes = _configuration.GetValue<int>("MailSync:TimeoutMinutes", 60);
            var alwaysForceFullSync = _configuration.GetValue<bool>("MailSync:AlwaysForceFullSync", false);

            // Per-account next-run scheduling state, keyed by account Id. Persists across
            // poll cycles so that intervals survive the short 60s polling cadence.
            var nextRunUtc = new Dictionary<int, DateTime>();
            var lastFullSyncUtc = new Dictionary<int, DateTime>();

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Starting mail sync process...");
                try
                {
                    // Step 1: Load the list of accounts in a disposable scope.
                    // Loaded fresh every cycle so that per-account interval changes made in the
                    // frontend take effect on the next cycle without a restart.
                    List<MailAccount> accounts;
                    using (var initScope = _serviceProvider.CreateScope())
                    {
                        var dbContext = initScope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();

                        var accountsForSync = await dbContext.MailAccounts
                            .Where(a => a.IsEnabled && a.Provider != ProviderType.IMPORT)
                            .ToListAsync(stoppingToken);

                        _logger.LogInformation($"Found {accountsForSync.Count} enabled accounts to sync");

                        if (alwaysForceFullSync)
                        {
                            _logger.LogInformation("AlwaysForceFullSync is enabled. Forcing full resync for all accounts.");
                            foreach (var account in accountsForSync)
                            {
                                account.LastSync = EpochUtc;
                            }
                            await dbContext.SaveChangesAsync();
                            dbContext.ChangeTracker.Clear();
                        }
                        else
                        {
                            _logger.LogInformation("AlwaysForceFullSync is disabled. Using quick sync for all accounts.");
                        }

                        accounts = await dbContext.MailAccounts
                            .AsNoTracking()
                            .Where(a => a.IsEnabled && a.Provider != ProviderType.IMPORT)
                            .ToListAsync(stoppingToken);
                    } // initScope disposed here

                    // Prune scheduling state for accounts that no longer exist / are disabled.
                    var activeIds = new HashSet<int>(accounts.Select(a => a.Id));
                    foreach (var id in nextRunUtc.Keys.Where(k => !activeIds.Contains(k)).ToList())
                        nextRunUtc.Remove(id);
                    foreach (var id in lastFullSyncUtc.Keys.Where(k => !activeIds.Contains(k)).ToList())
                        lastFullSyncUtc.Remove(id);

                    var nowUtc = DateTime.UtcNow;

                    // Step 2: Sync each account whose next-run is due, in its own scope so
                    // the scoped DbContext (and any tracked LOH payloads) are released between
                    // accounts.
                    foreach (var account in accounts)
                    {
                        if (stoppingToken.IsCancellationRequested)
                            break;

                        // Initialise scheduling state for new accounts. A brand-new account
                        // has LastSync == Epoch, so the first sync is a full sync anyway; run
                        // it immediately.
                        if (!nextRunUtc.ContainsKey(account.Id))
                            nextRunUtc[account.Id] = nowUtc;

                        if (nowUtc < nextRunUtc[account.Id])
                            continue;

                        // Determine effective intervals.
                        var effectiveSyncInterval = account.SyncIntervalMinutes ?? defaultSyncIntervalMinutes;
                        if (effectiveSyncInterval < 1) effectiveSyncInterval = 1;

                        // Schedule the next normal sync from "now" (will be updated to now+interval
                        // after the sync completes below).
                        nextRunUtc[account.Id] = nowUtc.AddMinutes(effectiveSyncInterval);

                        // Auto full-sync scheduling. The effective full-sync interval is the
                        // per-account value if set, otherwise the global default from appsettings.
                        // When both are null (the default) no automatic full sync runs — only the
                        // manual resync button and AlwaysForceFullSync remain.
                        var performFullSync = false;
                        if (!alwaysForceFullSync)
                        {
                            int? effectiveFullSyncIntervalHours = account.FullSyncIntervalHours ?? defaultFullSyncIntervalHours;
                            if (effectiveFullSyncIntervalHours.HasValue)
                            {
                                var fullIntervalHours = effectiveFullSyncIntervalHours.Value;
                                if (fullIntervalHours < 1) fullIntervalHours = 1;

                                // Seed lastFullSyncUtc from the DB column the first time we see
                                // this account.
                                if (!lastFullSyncUtc.ContainsKey(account.Id))
                                    lastFullSyncUtc[account.Id] = account.LastFullSync ?? EpochUtc;

                                var nextFullRun = lastFullSyncUtc[account.Id].AddHours(fullIntervalHours);
                                if (nowUtc >= nextFullRun)
                                    performFullSync = true;
                            }
                        }

                        if (performFullSync)
                        {
                            _logger.LogInformation(
                                "Scheduling automatic full sync for account {AccountName} (effective FullSyncIntervalHours={Hours})",
                                account.Name, (account.FullSyncIntervalHours ?? defaultFullSyncIntervalHours).Value);
                        }

                        try
                        {
                            using var accountScope = _serviceProvider.CreateScope();
                            var accountServices = accountScope.ServiceProvider;

                            var providerFactory = accountServices.GetRequiredService<MailArchiver.Services.Factories.ProviderEmailServiceFactory>();
                            var graphEmailService = accountServices.GetRequiredService<IGraphEmailService>();
                            var syncJobService = accountServices.GetRequiredService<ISyncJobService>(); // singleton, same instance
                            var bandwidthService = accountServices.GetRequiredService<IBandwidthService>();
                            var bandwidthOptions = accountServices.GetRequiredService<IOptions<BandwidthTrackingOptions>>();

                            // Pre-sync bandwidth limit check
                            if (bandwidthOptions.Value.Enabled)
                            {
                                var limitReached = await bandwidthService.IsLimitReachedAsync(account.Id);
                                if (limitReached)
                                {
                                    var status = await bandwidthService.GetStatusAsync(account.Id);
                                    _logger.LogWarning("Skipping sync for account {AccountName} - bandwidth limit reached. " +
                                        "Downloaded: {DownloadedMB:F2} MB / {LimitMB:F2} MB. Reset at: {ResetTime}",
                                        account.Name,
                                        status.BytesDownloaded / (1024.0 * 1024.0),
                                        status.DailyLimitBytes / (1024.0 * 1024.0),
                                        status.ResetTime);
                                    continue;
                                }
                            }

                            using var accountCts = new CancellationTokenSource(TimeSpan.FromMinutes(syncTimeoutMinutes));
                            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(accountCts.Token, stoppingToken);

                            // If an automatic full sync is due, reset the watermark so the
                            // provider's sync code treats this as a full sync. This mirrors the
                            // manual ResyncAccountAsync behaviour. Persist the reset so a crash
                            // / timeout does not silently lose the full-sync trigger.
                            if (performFullSync)
                            {
                                using (var resetScope = _serviceProvider.CreateScope())
                                {
                                    var resetCtx = resetScope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();
                                    var dbAccount = await resetCtx.MailAccounts.FindAsync(account.Id);
                                    if (dbAccount != null)
                                    {
                                        dbAccount.LastSync = EpochUtc;
                                        await resetCtx.SaveChangesAsync(stoppingToken);
                                    }
                                }
                                account.LastSync = EpochUtc;
                            }

                            var jobId = await syncJobService.StartSyncAsync(account.Id, account.Name, account.LastSync);

                            if (jobId == null)
                            {
                                _logger.LogWarning("Skipping sync for account {AccountId} ({AccountName}) - account no longer exists or is disabled",
                                    account.Id, account.Name);
                                continue;
                            }

                            syncJobService.UpdateJobProgress(jobId, job =>
                            {
                                job.CancellationTokenSource = accountCts;
                            });

                            _logger.LogInformation("Started sync job {JobId} for account {AccountName} with cancellation token",
                                jobId, account.Name);

                            if (account.Provider == ProviderType.M365)
                            {
                                _logger.LogInformation("Using Microsoft Graph API for M365 account: {AccountName}", account.Name);
                                await graphEmailService.SyncMailAccountAsync(account, jobId);
                            }
                            else
                            {
                                _logger.LogInformation("Using IMAP for account: {AccountName}", account.Name);
                                var provider = await providerFactory.GetServiceForAccountAsync(account.Id);
                                await provider.SyncMailAccountAsync(account, jobId);
                            }

                            // NOTE: Checkpoint clearing is handled by SyncMailAccountAsync itself.
                            _logger.LogInformation("Mail sync completed for account: {AccountName}", account.Name);

                            // After a successful (full) sync, record LastFullSync so the next
                            // automatic full sync is scheduled correctly.
                            if (performFullSync)
                            {
                                lastFullSyncUtc[account.Id] = nowUtc;
                                try
                                {
                                    using var markScope = _serviceProvider.CreateScope();
                                    var markCtx = markScope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();
                                    var dbAccount = await markCtx.MailAccounts.FindAsync(account.Id);
                                    if (dbAccount != null)
                                    {
                                        dbAccount.LastFullSync = nowUtc;
                                        await markCtx.SaveChangesAsync(stoppingToken);
                                    }
                                }
                                catch (Exception markEx)
                                {
                                    _logger.LogWarning(markEx,
                                        "Failed to persist LastFullSync for account {AccountId} (non-fatal)",
                                        account.Id);
                                }
                            }

                            // Sofort-Refresh des Speichercaches fuer diesen Account
                            try
                            {
                                var storageService = accountServices.GetRequiredService<IAccountStorageService>();
                                await storageService.RefreshAccountStorageAsync(account.Id);
                            }
                            catch (Exception storageEx)
                            {
                                _logger.LogDebug(storageEx, "Storage cache refresh after sync failed (non-fatal) for account {AccountId}", account.Id);
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            _logger.LogWarning("Sync for account {AccountName} timed out after {Timeout} minutes",
                                account.Name, syncTimeoutMinutes);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error syncing mail account {AccountName}: {Message}",
                                account.Name, ex.Message);
                        }
                        // accountScope disposed here - DbContext + any leftover tracked entities gone

                        // MEMORY FIX: After every account, request a compacting full GC including the
                        // Large Object Heap. Email attachments (>85 KB) live on the LOH which is
                        // never compacted by default; without this step .NET happily keeps the
                        // freed space resident, so the OS-visible working set never shrinks after
                        // a cancel or between accounts.
                        try
                        {
                            System.Runtime.GCSettings.LargeObjectHeapCompactionMode =
                                System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
                            GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
                            GC.WaitForPendingFinalizers();
                            GC.Collect();
                            _logger.LogDebug("Memory after account {AccountName}: {Memory}",
                                account.Name, MemoryMonitor.GetMemoryUsageFormatted());
                        }
                        catch (Exception gcEx)
                        {
                            _logger.LogDebug(gcEx, "Post-account GC compaction failed (non-fatal)");
                        }

                        await Task.Delay(TimeSpan.FromSeconds(InterAccountDelaySeconds), stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during mail sync process: {Message}", ex.Message);
                }

                _logger.LogInformation("Mail sync cycle completed. Waiting for next poll.");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(PollIntervalSeconds), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Shutdown during the inter-poll delay — exit gracefully.
                    break;
                }
            }
        }
    }
}
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
            
            var syncIntervalMinutes = _configuration.GetValue<int>("MailSync:IntervalMinutes", 15);
            var syncTimeoutMinutes = _configuration.GetValue<int>("MailSync:TimeoutMinutes", 60);
            var alwaysForceFullSync = _configuration.GetValue<bool>("MailSync:AlwaysForceFullSync", false);

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Starting mail sync process...");
                try
                {
                    // MEMORY FIX (memory-leak-graph-sync):
                    // Previously ONE scope was used for the whole sync cycle covering all accounts.
                    // The scoped DbContext therefore retained its change tracker (and any
                    // accidentally tracked attachment byte[] arrays) until the next sync cycle
                    // started, which is why users reported that memory stayed high even after a
                    // cancel/pause. By using a short-lived scope for the initial account list and
                    // a fresh scope per account we ensure the DbContext – and all the LOH-sized
                    // payloads referenced by it – gets disposed as soon as an account finishes
                    // syncing.

                    // Step 1: Load the list of accounts in a disposable scope.
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
                                account.LastSync = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
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

                    // Step 2: Sync each account in its own scope so the scoped DbContext is
                    // released (and its memory reclaimed) between accounts.
                    foreach (var account in accounts)
                    {
                        if (stoppingToken.IsCancellationRequested)
                            break;

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

                        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during mail sync process: {Message}", ex.Message);
                }

                _logger.LogInformation("Mail sync completed. Waiting for next sync cycle.");
                await Task.Delay(TimeSpan.FromMinutes(syncIntervalMinutes), stoppingToken);
            }
        }
    }
}
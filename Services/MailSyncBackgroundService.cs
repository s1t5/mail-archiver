using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services.Shared;
using MailArchiver.Services.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace MailArchiver.Services
{
    public class MailSyncBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MailSyncBackgroundService> _logger;
        private readonly IConfiguration _configuration;

        // Longest the tick loop waits between passes. A finishing sync wakes it early (see
        // slotFreed in ExecuteAsync), so this bounds the idle cadence, not the reaction time
        // to a freed slot. Short enough that per-account intervals (down to 1 minute) are
        // respected reasonably, long enough to avoid busy-waiting.
        private const int PollIntervalSeconds = 60;
        // How long shutdown waits for syncs that are still running before giving up on them.
        private const int ShutdownGraceSeconds = 30;
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
            // 0 or negative = no timeout; the fallback matches MailSyncOptions.
            var syncTimeoutMinutes = _configuration.GetValue<int>("MailSync:TimeoutMinutes", 0);
            var alwaysForceFullSync = _configuration.GetValue<bool>("MailSync:AlwaysForceFullSync", false);
            // How many account syncs may run at the same time. Not per poll cycle any more: the
            // tick dispatches into free slots and returns, so a slot is refilled by the next tick
            // rather than waiting for a whole batch. A value of 1 keeps syncs sequential.
            var maxConcurrentSyncs = _configuration.GetValue<int>("MailSync:MaxConcurrentSyncs", 1);
            if (maxConcurrentSyncs < 1) maxConcurrentSyncs = 1;
            // Optional stagger delay applied at the end of each account sync task.
            // Throttles slot turnover when MaxConcurrentSyncs > 1 (it does not prevent
            // the initial burst of the first N tasks starting together).
            var interAccountDelaySeconds = _configuration.GetValue<int>("MailSync:InterAccountDelaySeconds", 0);
            if (interAccountDelaySeconds < 0) interAccountDelaySeconds = 0;

            // Per-account next-run scheduling state, keyed by account Id. Persists across
            // poll cycles so that intervals survive the short 60s polling cadence. Uses
            // ConcurrentDictionary because multiple sync tasks may read/write in parallel
            // when MaxConcurrentSyncs > 1.
            var nextRunUtc = new ConcurrentDictionary<int, DateTime>();
            var lastFullSyncUtc = new ConcurrentDictionary<int, DateTime>();

            // The slots and who is holding one. syncSlots caps how many syncs run at once;
            // inFlight is the guard that keeps a tick from dispatching an account that is already
            // running, and doubles as the handle the shutdown path waits on.
            // Deliberately not disposed: a sync task can outlive this method when shutdown gives up
            // waiting, and it releases its slot in a finally. Disposing here would make that throw
            // inside a finally, on a task nobody is observing.
            var syncSlots = new SemaphoreSlim(maxConcurrentSyncs, maxConcurrentSyncs);
            var inFlight = new ConcurrentDictionary<int, Task>();

            // A slot being released wakes the tick early. Without this the loop would sit out its
            // full idle delay even though a slot - and possibly a queue of due accounts - is
            // waiting; with the default MaxConcurrentSyncs of 1 a backlog drained at one account
            // per minute instead of back-to-back. Re-armed with a fresh source after every wait
            // and before the next dispatch pass, so every release can signal - including one
            // that lands while the tick is mid-pass. A release racing the re-arm itself is
            // still covered: the finisher frees the semaphore before signaling, so the pass
            // sees that slot directly.
            var slotFreed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            // ISyncJobService is a singleton, so it can be resolved once here. It knows about syncs
            // this loop did not start - a manual sync from the account page - which inFlight cannot.
            var syncJobs = _serviceProvider.GetRequiredService<ISyncJobService>();

            // With a single slot the syncs are sequential, so the blocking compaction can run after
            // each account exactly as before. With more, it waits for the last one to finish.
            var compactAfterEachAccount = maxConcurrentSyncs == 1;

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
                        nextRunUtc.TryRemove(id, out _);
                    foreach (var id in lastFullSyncUtc.Keys.Where(k => !activeIds.Contains(k)).ToList())
                        lastFullSyncUtc.TryRemove(id, out _);

                    var nowUtc = DateTime.UtcNow;

                    // Seed scheduling state for accounts seen for the first time. A brand-new
                    // account has LastSync == Epoch, so its first sync is a full sync anyway; run
                    // it immediately.
                    foreach (var account in accounts)
                        nextRunUtc.TryAdd(account.Id, nowUtc);

                    // Step 2: what may start now, and in which order. The rule sits in
                    // SyncDispatchPlanner because both halves of it are easy to get wrong once the
                    // tick stops waiting for the batch: an account already running must not be
                    // started twice, and with more accounts due than slots free the most overdue one
                    // has to go first or it never gets a turn.
                    // Both sources of "already running" have to go in. inFlight is what makes the
                    // guard atomic against this loop itself, but it only knows the syncs this loop
                    // started; a manual sync from the account page is invisible to it. Without
                    // IsAccountSyncing the tick dispatches such an account, StartSyncAsync refuses
                    // it - correctly, nothing syncs twice - and the refusal surfaces as an error
                    // log plus an interval silently skipped.
                    var running = inFlight.Keys.ToHashSet();
                    foreach (var account in accounts)
                    {
                        if (syncJobs.IsAccountSyncing(account.Id))
                            running.Add(account.Id);
                    }

                    var dispatchOrder = SyncDispatchPlanner.SelectDueAccounts(
                        accounts.Select(a => (a.Id, nextRunUtc[a.Id])),
                        nowUtc,
                        running);

                    var accountsById = accounts.ToDictionary(a => a.Id);

                    // Step 3: hand due accounts to free slots and move on. The tick deliberately
                    // does not wait for them. It used to: one six-hour mailbox held the whole cycle
                    // open, and every account that came due meanwhile - including the ones that had
                    // finished minutes in - waited for it, because nothing was re-evaluated until the
                    // last task returned. Slots are refilled on the next tick as they come free.
                    if (dispatchOrder.Count > 0)
                    {
                        _logger.LogInformation(
                            "{Count} account(s) due, {Free} of {Max} sync slot(s) free",
                            dispatchOrder.Count, syncSlots.CurrentCount, maxConcurrentSyncs);
                    }

                    foreach (var accountId in dispatchOrder)
                    {
                        if (stoppingToken.IsCancellationRequested)
                            break;

                        // No free slot: leave the rest to the next tick instead of queueing them.
                        // Queueing would freeze the order chosen now, and the tick re-sorts by how
                        // overdue an account is, so waiting a minute keeps that priority honest.
                        if (!syncSlots.Wait(0))
                            break;

                        var account = accountsById[accountId];

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

                        // The in-flight entry is the guard against starting an account twice, so it
                        // has to exist before the work does. TryAdd is what makes that atomic.
                        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                        if (!inFlight.TryAdd(account.Id, completion.Task))
                        {
                            syncSlots.Release();
                            continue;
                        }

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await SyncOneAccountAsync(
                                    account, performFullSync, lastFullSyncUtc, syncTimeoutMinutes,
                                    compactAfterEachAccount, interAccountDelaySeconds, stoppingToken);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Unhandled error in the sync task for account {AccountName}: {Message}",
                                    account.Name, ex.Message);
                            }
                            finally
                            {
                                // Whatever happened: schedule the next run from the end of this one.
                                // The batch loop scheduled it at dispatch time, which for any account
                                // whose sync outlasts its own interval means it is due again the moment
                                // it finishes — so it would walk straight back into a slot instead of
                                // letting the queue move.
                                nextRunUtc[account.Id] = DateTime.UtcNow.AddMinutes(
                                    EffectiveSyncIntervalMinutes(account, defaultSyncIntervalMinutes));

                                inFlight.TryRemove(account.Id, out _);
                                syncSlots.Release();

                                // Wake the tick early so it can hand the freed slot to the next
                                // due account without sitting out the idle delay. TrySetResult:
                                // the tick re-arms the source before its next dispatch pass, so
                                // a finish racing the re-arm is still covered - the semaphore
                                // above was already freed, so the pass sees the slot directly.
                                slotFreed.TrySetResult();

                                completion.SetResult();

                                // The batch loop compacted the LOH once after every cycle. There is no
                                // cycle boundary any more, so the equivalent moment is the one where
                                // nothing is left running. Two tasks finishing together may both see an
                                // empty dictionary and compact twice; that is wasteful, not wrong.
                                if (!compactAfterEachAccount && inFlight.IsEmpty)
                                {
                                    CompactLargeObjectHeap("after the last in-flight sync");
                                }
                            }
                        }, CancellationToken.None);
                    }

                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Host shutdown while syncs were in flight — exit gracefully.
                    _logger.LogInformation("Mail sync process cancelled due to shutdown.");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during mail sync process: {Message}", ex.Message);
                }

                _logger.LogDebug("Mail sync tick done, {InFlight} sync(s) in flight. Waiting for next poll.",
                    inFlight.Count);

                // Wake early when a slot came free so a backlog drains back-to-back; otherwise
                // sit out the idle cadence. WhenAny returns the winner without observing its
                // status - it never rethrows - so on shutdown the cancelled delay simply wins
                // the race and the loop exits through the while condition below.
                await Task.WhenAny(
                    slotFreed.Task,
                    Task.Delay(TimeSpan.FromSeconds(PollIntervalSeconds), stoppingToken));

                // Re-arm before the next pass, not after it: a signal landing while the tick is
                // mid-dispatch would otherwise hit the already-completed source and be lost,
                // making a due account wait out the full idle delay.
                slotFreed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

                if (stoppingToken.IsCancellationRequested)
                    break;
            }

            // Shutdown. The syncs do not observe the host token — that would abort them mid-folder
            // and is a separate decision — so give them a bounded moment to end on their own rather
            // than tearing the process down underneath an open IMAP session.
            var stillRunning = inFlight.Values.ToArray();
            if (stillRunning.Length > 0)
            {
                _logger.LogInformation("Shutdown: waiting up to {Seconds}s for {Count} in-flight sync(s)",
                    ShutdownGraceSeconds, stillRunning.Length);
                await Task.WhenAny(
                    Task.WhenAll(stillRunning),
                    Task.Delay(TimeSpan.FromSeconds(ShutdownGraceSeconds), CancellationToken.None));
            }

            _logger.LogInformation("Mail Sync Background Service is stopping.");
        }

        /// <summary>
        /// Ends a job whose sync never finished, so it cannot sit on Running for the life of the
        /// process. Only a job that is still Running is touched: a sync that ended itself as
        /// TimedOut, Cancelled or Failed has already said something more precise, and overwriting
        /// that would throw away the one piece of information worth keeping.
        /// </summary>
        private void FailUnfinishedJob(string? jobId, string? reason)
        {
            if (jobId == null)
                return;

            try
            {
                var syncJobs = _serviceProvider.GetRequiredService<ISyncJobService>();
                if (SyncJobCompletion.NeedsClosing(syncJobs.GetJob(jobId)?.Status))
                {
                    syncJobs.CompleteJob(jobId, false, reason);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not end unfinished sync job {JobId}", jobId);
            }
        }

        /// <summary>
        /// Syncs one account. Lifted out of the former Parallel.ForEachAsync lambda so the scheduler
        /// around it can be read on its own. The body is unchanged apart from one thing: LastFullSync
        /// is stamped with the moment the sync finished rather than the moment its cycle began.
        /// </summary>
        private async Task SyncOneAccountAsync(
            MailAccount account,
            bool performFullSync,
            ConcurrentDictionary<int, DateTime> lastFullSyncUtc,
            int syncTimeoutMinutes,
            bool compactAfterEachAccount,
            int interAccountDelaySeconds,
            CancellationToken ct)
        {
            // Hoisted out of the try so the catch blocks can end the job. A sync that dies on an
            // unhandled exception used to leave its SyncJob on Running for good: CleanupOldJobs only
            // removes jobs that carry a completion timestamp, so nothing ever cleared it. The Jobs
            // page kept showing it as running, IsAccountSyncing kept answering true for that account
            // - which the account list and the dashboard render as "sync in progress" - and the
            // scheduler's own running check would have locked the account out of every future tick.
            string? jobId = null;

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
                        return;
                    }
                }

                // A non-positive timeout means "no timeout": the source is still
                // created so a manual cancel from the UI has something to fire, it
                // just never trips on its own.
                using var accountCts = syncTimeoutMinutes > 0
                    ? new CancellationTokenSource(TimeSpan.FromMinutes(syncTimeoutMinutes))
                    : new CancellationTokenSource();

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
                            await resetCtx.SaveChangesAsync(ct);
                        }
                    }
                    account.LastSync = EpochUtc;
                }

                jobId = await syncJobService.StartSyncAsync(account.Id, account.Name, account.LastSync);

                if (jobId == null)
                {
                    _logger.LogWarning("Skipping sync for account {AccountId} ({AccountName}) - account no longer exists or is disabled",
                        account.Id, account.Name);
                    return;
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
                    await graphEmailService.SyncMailAccountAsync(account, jobId, accountCts.Token);
                }
                else
                {
                    _logger.LogInformation("Using IMAP for account: {AccountName}", account.Name);
                    var provider = await providerFactory.GetServiceForAccountAsync(account.Id);
                    await provider.SyncMailAccountAsync(account, jobId, accountCts.Token);
                }

                // NOTE: Checkpoint clearing is handled by SyncMailAccountAsync itself.
                _logger.LogInformation("Mail sync completed for account: {AccountName}", account.Name);

                // After a successful (full) sync, record LastFullSync so the next
                // automatic full sync is scheduled correctly. Stamped with the moment the sync
                // finished, not the moment the cycle that dispatched it began: on a mailbox whose
                // full sync takes hours the difference is the whole run, and dating it from the
                // start makes the next one fall due that much sooner.
                if (performFullSync)
                {
                    var completedUtc = DateTime.UtcNow;
                    lastFullSyncUtc[account.Id] = completedUtc;
                    try
                    {
                        using var markScope = _serviceProvider.CreateScope();
                        var markCtx = markScope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();
                        var dbAccount = await markCtx.MailAccounts.FindAsync(account.Id);
                        if (dbAccount != null)
                        {
                            dbAccount.LastFullSync = completedUtc;
                            await markCtx.SaveChangesAsync(ct);
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
                // The sync timeout and a UI cancel are no longer delivered as
                // OperationCanceledException — the sync loops poll both signals
                // through SyncInterruption and end the job as TimedOut/Failed
                // themselves. If one surfaces here anyway, it did not come
                // from either of those mechanisms.
                _logger.LogWarning("Sync for account {AccountName} was cancelled unexpectedly",
                    account.Name);
                FailUnfinishedJob(jobId, "Sync was cancelled unexpectedly");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing mail account {AccountName}: {Message}",
                    account.Name, ex.Message);
                FailUnfinishedJob(jobId, ex.Message);
            }
            // accountScope disposed here - DbContext + any leftover tracked entities gone

            // MEMORY FIX: Only when running sequentially, request a compacting full GC
            // after every account (see CompactLargeObjectHeap). With more than one slot a
            // blocking full GC would pause every other in-flight sync, so in that case the
            // caller compacts once the last of them has finished instead.
            if (compactAfterEachAccount)
            {
                CompactLargeObjectHeap($"after account {account.Name}");
            }

            if (interAccountDelaySeconds > 0)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(interAccountDelaySeconds), ct);
                }
                catch (OperationCanceledException)
                {
                    // Shutdown during the inter-account delay — exit gracefully.
                }
            }
        }

        /// <summary>
        /// The account's own sync interval when it sets one, otherwise the installation default.
        /// Never below one minute, because a zero would turn the scheduler into a busy loop.
        /// </summary>
        private static int EffectiveSyncIntervalMinutes(MailAccount account, int defaultSyncIntervalMinutes)
        {
            var minutes = account.SyncIntervalMinutes ?? defaultSyncIntervalMinutes;
            return minutes < 1 ? 1 : minutes;
        }

        // MEMORY FIX: Request a compacting full GC including the Large Object Heap.
        // Email attachments (>85 KB) live on the LOH which is never compacted by
        // default; without this step .NET happily keeps the freed space resident,
        // so the OS-visible working set never shrinks after a cancel or between
        // accounts/cycles.
        private void CompactLargeObjectHeap(string context)
        {
            try
            {
                System.Runtime.GCSettings.LargeObjectHeapCompactionMode =
                    System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
                GC.WaitForPendingFinalizers();
                GC.Collect();
                _logger.LogDebug("Memory {Context}: {Memory}",
                    context, MemoryMonitor.GetMemoryUsageFormatted());
            }
            catch (Exception gcEx)
            {
                _logger.LogDebug(gcEx, "GC compaction failed (non-fatal)");
            }
        }
    }
}
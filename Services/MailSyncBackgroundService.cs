using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services;
using Microsoft.EntityFrameworkCore;

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
                    using var scope = _serviceProvider.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();
                    var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                    var graphEmailService = scope.ServiceProvider.GetRequiredService<IGraphEmailService>();
                    var syncJobService = scope.ServiceProvider.GetRequiredService<ISyncJobService>();

                    var accounts = await dbContext.MailAccounts
                        .Where(a => a.IsEnabled && a.Provider != ProviderType.IMPORT)
                        .ToListAsync(stoppingToken);

                    _logger.LogInformation($"Found {accounts.Count} enabled accounts to sync");

                    // If AlwaysForceFullSync is enabled, reset LastSync for all accounts to force full resync
                    if (alwaysForceFullSync)
                    {
                        _logger.LogInformation("AlwaysForceFullSync is enabled. Forcing full resync for all accounts.");
                        foreach (var account in accounts)
                        {
                            account.LastSync = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                        }
                        await dbContext.SaveChangesAsync();
                    }
                    else
                    { 
                        _logger.LogInformation("AlwaysForceFullSync is disabled. Using quick sync for all accounts.");
                    }

                    foreach (var account in accounts)
                    {
                        try
                        {
                            using var accountCts = new CancellationTokenSource(TimeSpan.FromMinutes(syncTimeoutMinutes));
                            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(accountCts.Token, stoppingToken);

                            // Start sync job tracking
                            var jobId = syncJobService.StartSync(account.Id, account.Name, account.LastSync);
                            
                            // Update job with cancellation token source
                            syncJobService.UpdateJobProgress(jobId, job =>
                            {
                                job.CancellationTokenSource = accountCts;
                            });
                            
                            _logger.LogInformation("Started sync job {JobId} for account {AccountName} with cancellation token", 
                                jobId, account.Name);
                            
                            // Route to appropriate service based on provider type
                            if (account.Provider == ProviderType.M365)
                            {
                                _logger.LogInformation("Using Microsoft Graph API for M365 account: {AccountName}", account.Name);
                                await graphEmailService.SyncMailAccountAsync(account, jobId);
                            }
                            else
                            {
                                _logger.LogInformation("Using IMAP for account: {AccountName}", account.Name);
                                await emailService.SyncMailAccountAsync(account, jobId);
                            }
                            
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

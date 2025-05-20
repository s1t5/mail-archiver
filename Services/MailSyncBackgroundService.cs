using MailArchiver.Data;
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
            var syncTimeoutMinutes = _configuration.GetValue<int>("MailSync:TimeoutMinutes", 60); // 1 Stunde Standardwert

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Starting mail sync process...");
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();
                    var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

                    // Only sync enabled accounts
                    var accounts = await dbContext.MailAccounts
                        .Where(a => a.IsEnabled)
                        .ToListAsync(stoppingToken);

                    _logger.LogInformation($"Found {accounts.Count} enabled accounts to sync");

                    foreach (var account in accounts)
                    {
                        try
                        {
                            // Benutze CancellationTokenSource für Timeout pro Account
                            using var accountCts = new CancellationTokenSource(TimeSpan.FromMinutes(syncTimeoutMinutes));
                            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(accountCts.Token, stoppingToken);

                            await emailService.SyncMailAccountAsync(account);
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

                        // Kleine Pause zwischen Konten, um Server zu entlasten
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
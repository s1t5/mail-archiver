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
                            await emailService.SyncMailAccountAsync(account);
                            _logger.LogInformation("Mail sync completed for account: {AccountName}", account.Name);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error syncing mail account {AccountName}: {Message}",
                                account.Name, ex.Message);
                        }
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
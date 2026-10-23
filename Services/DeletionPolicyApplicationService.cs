using MailArchiver.Data;
using MailArchiver.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MailArchiver.Services
{
    /// <summary>
    /// Background service that applies the configured deletion policy to all
    /// existing archived emails by setting IsLocked in batches. Runs once on
    /// startup and then completes. Progress is logged via ILogger only (not on
    /// the frontend Logs page). Keeps the application startup fast by moving
    /// the potentially expensive row-by-row UPDATE out of the startup path.
    /// </summary>
    public class DeletionPolicyApplicationService : BackgroundService
    {
        private readonly ILogger<DeletionPolicyApplicationService> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IOptions<DeletionPolicyOptions> _deletionPolicy;

        private const int BatchSize = 5000;
        private const int DelayMs = 100;
        private const int StartupDelaySeconds = 10;

        public DeletionPolicyApplicationService(
            ILogger<DeletionPolicyApplicationService> logger,
            IServiceScopeFactory serviceScopeFactory,
            IOptions<DeletionPolicyOptions> deletionPolicy)
        {
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
            _deletionPolicy = deletionPolicy;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var deletionAllowed = _deletionPolicy.Value.DeletionAllowed;
            var targetLocked = !deletionAllowed;
            var targetLiteral = targetLocked ? "TRUE" : "FALSE";

            _logger.LogInformation("DeletionPolicy background apply scheduled: target IsLocked={TargetLocked} (DeletionAllowed={DeletionAllowed})",
                targetLocked, deletionAllowed);

            // Allow other startup services to initialize before starting the bulk update.
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(StartupDelaySeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (stoppingToken.IsCancellationRequested)
                return;

            _logger.LogInformation("DeletionPolicy background apply started: updating rows where IsLocked IS DISTINCT FROM {Target}",
                targetLiteral);

            var totalUpdated = 0;
            var batches = 0;
            var lastId = 0;

            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();

                while (!stoppingToken.IsCancellationRequested)
                {
                    // Keyset pagination via Id for efficient large-table scanning.
                    var batchIds = await context.ArchivedEmails
                        .Where(e => e.Id > lastId && e.IsLocked != targetLocked)
                        .OrderBy(e => e.Id)
                        .Select(e => e.Id)
                        .Take(BatchSize)
                        .ToListAsync(stoppingToken);

                    if (batchIds.Count == 0)
                        break;

                    // Batch UPDATE via raw SQL with IN-list.
                    var idList = string.Join(",", batchIds);
                    var rowsAffected = await context.Database.ExecuteSqlRawAsync(
                        $@"UPDATE mail_archiver.""ArchivedEmails"" SET ""IsLocked"" = {targetLiteral}
                           WHERE ""Id"" IN ({idList});",
                        stoppingToken);

                    totalUpdated += rowsAffected;
                    batches++;
                    lastId = batchIds[^1];

                    // Log progress every 10 batches or on first batch.
                    if (batches == 1 || batches % 10 == 0)
                    {
                        _logger.LogInformation("DeletionPolicy background apply progress: {Batches} batches, {Updated} rows updated so far",
                            batches, totalUpdated);
                    }

                    // Small pause to avoid burdening the database and concurrent syncs.
                    try
                    {
                        await Task.Delay(DelayMs, stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }

                _logger.LogInformation("DeletionPolicy background apply completed: updated {TotalUpdated} rows in {Batches} batches",
                    totalUpdated, batches);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("DeletionPolicy background apply cancelled after {Batches} batches ({Updated} rows updated)",
                    batches, totalUpdated);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeletionPolicy background apply failed after {Batches} batches ({Updated} rows updated)",
                    batches, totalUpdated);
            }
        }
    }
}
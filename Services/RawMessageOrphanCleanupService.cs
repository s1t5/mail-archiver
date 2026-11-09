using MailArchiver.Models;
using MailArchiver.Services.Shared;
using Microsoft.Extensions.Options;

namespace MailArchiver.Services
{
    /// <summary>
    /// Runs <see cref="RawMessageOrphanCollector"/> on a schedule, but only when the filesystem
    /// backend is in use - with the Postgres backend no files exist and the run would be an
    /// empty directory walk.
    /// </summary>
    public class RawMessageOrphanCleanupService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IOptions<RawMessageOptions> _options;
        private readonly ILogger<RawMessageOrphanCleanupService> _logger;

        public RawMessageOrphanCleanupService(
            IServiceScopeFactory scopeFactory,
            IOptions<RawMessageOptions> options,
            ILogger<RawMessageOrphanCleanupService> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var fs = _options.Value.FileSystem;

            if (!fs.OrphanCleanupEnabled)
            {
                _logger.LogInformation("Original message orphan cleanup is disabled");
                return;
            }

            if (!_options.Value.WritesToFileSystem)
            {
                // Rows can still be filesystem-backed after a switch to Postgres (until the
                // migration has run), so look once and then stop if there is nothing to do.
                if (!await HasFileSystemRowsAsync(stoppingToken))
                {
                    _logger.LogDebug("Original message orphan cleanup: no filesystem-backed originals, skipping");
                    return;
                }
            }

            try
            {
                // Let startup and any pending migration finish first.
                await Task.Delay(TimeSpan.FromSeconds(fs.OrphanCleanupStartupDelaySeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            var interval = TimeSpan.FromHours(Math.Max(1, fs.OrphanCleanupIntervalHours));
            var grace = TimeSpan.FromHours(Math.Max(0, fs.OrphanCleanupGraceHours));

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var collector = scope.ServiceProvider.GetRequiredService<RawMessageOrphanCollector>();
                    await collector.CollectAsync(grace, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Original message orphan cleanup failed: {Message}", ex.Message);
                }

                try
                {
                    await Task.Delay(interval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        private async Task<bool> HasFileSystemRowsAsync(CancellationToken token)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<MailArchiver.Data.MailArchiverDbContext>();
                return await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                    .AnyAsync(context.RawMessages.Where(r => r.StorageType == RawMessageStorage.FileSystem), token);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not check for filesystem-backed originals");
                return false;
            }
        }
    }
}

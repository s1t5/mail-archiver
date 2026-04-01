using MailArchiver.Data;
using MailArchiver.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MailArchiver.Services
{
    /// <summary>
    /// Service for bandwidth tracking and rate limit management.
    /// Implements IBandwidthService for tracking IMAP bandwidth usage and managing sync checkpoints.
    /// </summary>
    public class BandwidthService : IBandwidthService
    {
        private readonly MailArchiverDbContext _context;
        private readonly ILogger<BandwidthService> _logger;
        private readonly BandwidthTrackingOptions _options;

        public BandwidthService(
            MailArchiverDbContext context,
            ILogger<BandwidthService> logger,
            IOptions<BandwidthTrackingOptions> options)
        {
            _context = context;
            _logger = logger;
            _options = options.Value;
        }

        #region Bandwidth Tracking

        /// <inheritdoc />
        public async Task<bool> IsLimitReachedAsync(int accountId)
        {
            if (!_options.Enabled)
            {
                return false;
            }

            var today = DateTime.UtcNow.Date;
            var usage = await GetOrCreateUsageRecordAsync(accountId, today);

            // Check if limit was marked as reached
            if (usage.LimitReached && usage.LimitResetTime.HasValue)
            {
                // Check if the reset time has passed
                if (DateTime.UtcNow >= usage.LimitResetTime.Value)
                {
                    // Reset time has passed, clear the flag
                    await ClearLimitReachedAsync(accountId);
                    return false;
                }

                _logger.LogWarning("Bandwidth limit reached for account {AccountId}. Reset at {ResetTime}. " +
                    "Downloaded: {DownloadedMB:F2} MB / {LimitMB:F2} MB",
                    accountId, usage.LimitResetTime, 
                    usage.BytesDownloaded / (1024.0 * 1024.0), 
                    _options.DailyLimitMb);
                return true;
            }

            // Check if current usage exceeds limit
            var limitBytes = _options.DailyLimitMb * 1024L * 1024L;
            if (usage.BytesDownloaded >= limitBytes)
            {
                await SetLimitReachedAsync(accountId);
                return true;
            }

            return false;
        }

        /// <inheritdoc />
        public async Task<BandwidthStatus> GetStatusAsync(int accountId)
        {
            var today = DateTime.UtcNow.Date;
            var usage = await GetOrCreateUsageRecordAsync(accountId, today);
            var limitBytes = _options.DailyLimitMb * 1024L * 1024L;

            return new BandwidthStatus
            {
                BytesDownloaded = usage.BytesDownloaded,
                BytesUploaded = usage.BytesUploaded,
                EmailsProcessed = usage.EmailsProcessed,
                DailyLimitBytes = limitBytes,
                PercentUsed = limitBytes > 0 ? (double)usage.BytesDownloaded / limitBytes * 100 : 0,
                LimitReached = usage.LimitReached,
                ResetTime = usage.LimitResetTime,
                TrackingEnabled = _options.Enabled
            };
        }

        /// <inheritdoc />
        public async Task<BandwidthUsage> TrackUsageAsync(int accountId, long bytesDownloaded, long bytesUploaded = 0, int emailsProcessed = 1)
        {
            if (!_options.Enabled)
            {
                // Return a dummy record if tracking is disabled
                return new BandwidthUsage
                {
                    MailAccountId = accountId,
                    Date = DateTime.UtcNow.Date
                };
            }

            var today = DateTime.UtcNow.Date;
            
            // Ensure the record exists
            await GetOrCreateUsageRecordAsync(accountId, today);

            // Use atomic SQL UPDATE to prevent lost updates under concurrent access.
            // This ensures BytesDownloaded = BytesDownloaded + @bytes at the DB level,
            // avoiding read-modify-write race conditions.
            var now = DateTime.UtcNow;
            await _context.Database.ExecuteSqlRawAsync(
                @"UPDATE mail_archiver.""BandwidthUsage"" 
                  SET ""BytesDownloaded"" = ""BytesDownloaded"" + {0}, 
                      ""BytesUploaded"" = ""BytesUploaded"" + {1}, 
                      ""EmailsProcessed"" = ""EmailsProcessed"" + {2}, 
                      ""UpdatedAt"" = {3} 
                  WHERE ""MailAccountId"" = {4} AND ""Date"" = {5}",
                bytesDownloaded, bytesUploaded, emailsProcessed, now, accountId, today);

            // Re-fetch the updated record to get accurate totals for logging
            var usage = await _context.BandwidthUsages
                .AsNoTracking()
                .FirstAsync(u => u.MailAccountId == accountId && u.Date == today);

            // Log warning if approaching limit
            var limitBytes = _options.DailyLimitMb * 1024L * 1024L;
            var percentUsed = limitBytes > 0 ? (double)usage.BytesDownloaded / limitBytes * 100 : 0;

            if (percentUsed >= _options.WarningThresholdPercent)
            {
                _logger.LogWarning("Bandwidth usage at {PercentUsed:F1}% for account {AccountId}. " +
                    "Downloaded: {DownloadedMB:F2} MB / {LimitMB:F2} MB",
                    percentUsed, accountId, 
                    usage.BytesDownloaded / (1024.0 * 1024.0), 
                    _options.DailyLimitMb);
            }

            _logger.LogDebug("Tracked bandwidth for account {AccountId}: +{BytesDownloaded} bytes downloaded, " +
                "{TotalMB:F2} MB total", accountId, bytesDownloaded, usage.BytesDownloaded / (1024.0 * 1024.0));

            return usage;
        }

        /// <inheritdoc />
        public async Task<(BandwidthUsage Usage, bool LimitReached)> TrackUsageAndCheckLimitAsync(int accountId, long bytesDownloaded, long bytesUploaded = 0, int emailsProcessed = 1)
        {
            if (!_options.Enabled)
            {
                return (new BandwidthUsage { MailAccountId = accountId, Date = DateTime.UtcNow.Date }, false);
            }

            // Track usage with atomic update
            var usage = await TrackUsageAsync(accountId, bytesDownloaded, bytesUploaded, emailsProcessed);

            // Check limit in the same call — no extra DB roundtrip needed since we already 
            // have the fresh usage record from TrackUsageAsync
            var limitBytes = _options.DailyLimitMb * 1024L * 1024L;
            
            // Check if already marked as reached
            if (usage.LimitReached && usage.LimitResetTime.HasValue)
            {
                if (DateTime.UtcNow >= usage.LimitResetTime.Value)
                {
                    await ClearLimitReachedAsync(accountId);
                    return (usage, false);
                }
                return (usage, true);
            }

            // Check if current usage exceeds limit
            if (usage.BytesDownloaded >= limitBytes)
            {
                await SetLimitReachedAsync(accountId);
                return (usage, true);
            }

            return (usage, false);
        }

        /// <inheritdoc />
        public async Task SetLimitReachedAsync(int accountId, DateTime? resetTime = null)
        {
            var today = DateTime.UtcNow.Date;
            var usage = await GetOrCreateUsageRecordAsync(accountId, today);

            usage.LimitReached = true;
            usage.LimitResetTime = resetTime ?? DateTime.UtcNow.AddHours(_options.PauseHoursOnLimit);
            usage.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogWarning("Bandwidth limit reached for account {AccountId}. Sync paused until {ResetTime}",
                accountId, usage.LimitResetTime);
        }

        /// <inheritdoc />
        public async Task ClearLimitReachedAsync(int accountId)
        {
            var today = DateTime.UtcNow.Date;
            var usage = await GetOrCreateUsageRecordAsync(accountId, today);

            usage.LimitReached = false;
            usage.LimitResetTime = null;
            usage.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Cleared bandwidth limit flag for account {AccountId}", accountId);
        }

        private async Task<BandwidthUsage> GetOrCreateUsageRecordAsync(int accountId, DateTime date)
        {
            var usage = await _context.BandwidthUsages
                .FirstOrDefaultAsync(u => u.MailAccountId == accountId && u.Date == date);

            if (usage == null)
            {
                usage = new BandwidthUsage
                {
                    MailAccountId = accountId,
                    Date = date,
                    BytesDownloaded = 0,
                    BytesUploaded = 0,
                    EmailsProcessed = 0,
                    LimitReached = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.BandwidthUsages.Add(usage);
                
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    // Race condition: another thread/request already created the record.
                    // Detach the failed entity and re-fetch from DB.
                    _context.Entry(usage).State = EntityState.Detached;
                    
                    usage = await _context.BandwidthUsages
                        .FirstOrDefaultAsync(u => u.MailAccountId == accountId && u.Date == date);
                    
                    if (usage == null)
                    {
                        // Should not happen, but re-throw if it does
                        throw;
                    }
                    
                    _logger.LogDebug("Resolved race condition in GetOrCreateUsageRecordAsync for account {AccountId}", accountId);
                }
            }

            return usage;
        }

        #endregion

        #region Checkpoint Management

        /// <inheritdoc />
        public async Task<SyncCheckpoint> GetOrCreateCheckpointAsync(int accountId, string folderName)
        {
            var checkpoint = await _context.SyncCheckpoints
                .FirstOrDefaultAsync(c => c.MailAccountId == accountId && c.FolderName == folderName);

            if (checkpoint == null)
            {
                checkpoint = new SyncCheckpoint
                {
                    MailAccountId = accountId,
                    FolderName = folderName,
                    ProcessedCount = 0,
                    IsCompleted = false,
                    BytesDownloaded = 0,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.SyncCheckpoints.Add(checkpoint);
                await _context.SaveChangesAsync();

                _logger.LogDebug("Created new sync checkpoint for account {AccountId}, folder {FolderName}",
                    accountId, folderName);
            }

            return checkpoint;
        }

        /// <inheritdoc />
        public async Task UpdateCheckpointAsync(int accountId, string folderName, DateTime? lastMessageDate, string? lastMessageId, long bytesDownloaded = 0)
        {
            var checkpoint = await GetOrCreateCheckpointAsync(accountId, folderName);

            checkpoint.LastMessageDate = lastMessageDate;
            checkpoint.LastMessageId = lastMessageId;
            checkpoint.ProcessedCount++;
            checkpoint.BytesDownloaded += bytesDownloaded;
            checkpoint.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogDebug("Updated checkpoint for account {AccountId}, folder {FolderName}: " +
                "Processed {Count} messages, {Bytes} bytes",
                accountId, folderName, checkpoint.ProcessedCount, checkpoint.BytesDownloaded);
        }

        /// <inheritdoc />
        public async Task MarkFolderCompletedAsync(int accountId, string folderName)
        {
            var checkpoint = await GetOrCreateCheckpointAsync(accountId, folderName);

            checkpoint.IsCompleted = true;
            checkpoint.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogDebug("Marked checkpoint as completed for account {AccountId}, folder {FolderName}",
                accountId, folderName);
        }

        /// <inheritdoc />
        public async Task ClearCheckpointsAsync(int accountId)
        {
            var checkpoints = await _context.SyncCheckpoints
                .Where(c => c.MailAccountId == accountId)
                .ToListAsync();

            if (checkpoints.Any())
            {
                _context.SyncCheckpoints.RemoveRange(checkpoints);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Cleared {Count} sync checkpoints for account {AccountId}",
                    checkpoints.Count, accountId);
            }
        }

        /// <inheritdoc />
        public async Task<List<SyncCheckpoint>> GetCheckpointsAsync(int accountId)
        {
            return await _context.SyncCheckpoints
                .Where(c => c.MailAccountId == accountId)
                .OrderBy(c => c.FolderName)
                .ToListAsync();
        }

        /// <inheritdoc />
        public async Task<bool> HasIncompleteCheckpointsAsync(int accountId)
        {
            return await _context.SyncCheckpoints
                .AnyAsync(c => c.MailAccountId == accountId && !c.IsCompleted);
        }

        #endregion

        #region Cleanup

        /// <inheritdoc />
        public async Task<int> CleanupOldBandwidthRecordsAsync(int olderThanDays = 7)
        {
            if (!_options.Enabled)
            {
                _logger.LogDebug("Bandwidth tracking is disabled, skipping cleanup");
                return 0;
            }

            var cutoffDate = DateTime.UtcNow.Date.AddDays(-olderThanDays);
            
            var oldRecords = await _context.BandwidthUsages
                .Where(u => u.Date < cutoffDate)
                .ToListAsync();

            if (oldRecords.Any())
            {
                _context.BandwidthUsages.RemoveRange(oldRecords);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Cleaned up {Count} old bandwidth usage records older than {Days} days (before {CutoffDate})",
                    oldRecords.Count, olderThanDays, cutoffDate);
                
                return oldRecords.Count;
            }

            _logger.LogDebug("No old bandwidth usage records to clean up (cutoff: {CutoffDate})", cutoffDate);
            return 0;
        }

        /// <inheritdoc />
        public async Task<int> CleanupOldCheckpointsAsync(int olderThanDays = 30)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-olderThanDays);
            
            // Only remove completed checkpoints that are old
            // Incomplete checkpoints should be kept for resume functionality
            var oldCheckpoints = await _context.SyncCheckpoints
                .Where(c => c.UpdatedAt < cutoffDate && c.IsCompleted)
                .ToListAsync();

            if (oldCheckpoints.Any())
            {
                _context.SyncCheckpoints.RemoveRange(oldCheckpoints);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Cleaned up {Count} old completed sync checkpoints older than {Days} days (before {CutoffDate})",
                    oldCheckpoints.Count, olderThanDays, cutoffDate);
                
                return oldCheckpoints.Count;
            }

            _logger.LogDebug("No old completed checkpoints to clean up (cutoff: {CutoffDate})", cutoffDate);
            return 0;
        }

        #endregion
    }
}

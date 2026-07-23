using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services;
using MailArchiver.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MailArchiver.Tests.Services;

/// <summary>
/// Integration tests for <see cref="BandwidthService"/> against the PostgreSQL Dev database.
/// Each test runs inside a rolled-back transaction so no rows persist.
/// </summary>
[Collection(TestDbFixture.CollectionName)]
public class BandwidthServiceTests
{
    private readonly TestDbFixture _fixture;
    public BandwidthServiceTests(TestDbFixture fixture) => _fixture = fixture;

    private static async Task<MailAccount> SeedAccountAsync(MailArchiverDbContext ctx)
    {
        var account = new MailAccount
        {
            Name = $"bw-{Guid.NewGuid():N}".Substring(0, 25),
            EmailAddress = $"{Guid.NewGuid():N}@test.local",
            Provider = ProviderType.IMAP,
            IsEnabled = true,
            LastSync = DateTime.UtcNow
        };
        ctx.MailAccounts.Add(account);
        await ctx.SaveChangesAsync();
        return account;
    }

    private static async Task SeedUsageAsync(MailArchiverDbContext ctx, int accountId,
        long bytes = 0, bool limitReached = false, DateTime? resetTime = null, DateTime? date = null)
    {
        ctx.BandwidthUsages.Add(new BandwidthUsage
        {
            MailAccountId = accountId,
            Date = date ?? DateTime.UtcNow.Date,
            BytesDownloaded = bytes,
            LimitReached = limitReached,
            LimitResetTime = resetTime,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();
    }

    // ============================================================
    // IsLimitReachedAsync
    // ============================================================

    [Fact]
    public async Task IsLimitReachedAsync_DisabledTracking_ReturnsFalse()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var svc = ServiceFactory.CreateBandwidthService(ctx, new BandwidthTrackingOptions { Enabled = false });
            Assert.False(await svc.IsLimitReachedAsync(acct.Id));
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task IsLimitReachedAsync_NoUsage_ReturnsFalse()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var svc = ServiceFactory.CreateBandwidthService(ctx);
            Assert.False(await svc.IsLimitReachedAsync(acct.Id));
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task IsLimitReachedAsync_OverLimit_ReturnsTrueAndSetsFlag()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var opts = new BandwidthTrackingOptions { Enabled = true, DailyLimitMb = 1, PauseHoursOnLimit = 1 };
            await SeedUsageAsync(ctx, acct.Id, bytes: 2 * 1024 * 1024); // 2 MB > 1 MB limit
            var svc = ServiceFactory.CreateBandwidthService(ctx, opts);
            Assert.True(await svc.IsLimitReachedAsync(acct.Id));
            var stored = await ctx.BandwidthUsages.AsNoTracking()
                .FirstAsync(u => u.MailAccountId == acct.Id && u.Date == DateTime.UtcNow.Date);
            Assert.True(stored.LimitReached);
            Assert.NotNull(stored.LimitResetTime);
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task IsLimitReachedAsync_FlagSetAndResetPassed_ClearsAndReturnsFalse()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var acct = await SeedAccountAsync(ctx);
            await SeedUsageAsync(ctx, acct.Id, bytes: 0, limitReached: true,
                resetTime: DateTime.UtcNow.AddMinutes(-1));
            var svc = ServiceFactory.CreateBandwidthService(ctx);
            Assert.False(await svc.IsLimitReachedAsync(acct.Id));
            var stored = await ctx.BandwidthUsages.AsNoTracking()
                .FirstAsync(u => u.MailAccountId == acct.Id && u.Date == DateTime.UtcNow.Date);
            Assert.False(stored.LimitReached);
            Assert.Null(stored.LimitResetTime);
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task IsLimitReachedAsync_FlagSetAndNotReset_ReturnsTrue()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var acct = await SeedAccountAsync(ctx);
            await SeedUsageAsync(ctx, acct.Id, bytes: 0, limitReached: true,
                resetTime: DateTime.UtcNow.AddHours(1));
            var svc = ServiceFactory.CreateBandwidthService(ctx);
            Assert.True(await svc.IsLimitReachedAsync(acct.Id));
        }
        finally { await scope.RollbackAsync(); }
    }

    // ============================================================
    // GetStatusAsync
    // ============================================================

    [Fact]
    public async Task GetStatusAsync_ReturnsCorrectValues()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var opts = new BandwidthTrackingOptions { Enabled = true, DailyLimitMb = 100 };
            await SeedUsageAsync(ctx, acct.Id, bytes: 50 * 1024 * 1024); // 50 MB
            var svc = ServiceFactory.CreateBandwidthService(ctx, opts);
            var status = await svc.GetStatusAsync(acct.Id);
            Assert.Equal(50 * 1024 * 1024L, status.BytesDownloaded);
            Assert.Equal(100 * 1024 * 1024L, status.DailyLimitBytes);
            Assert.True(status.PercentUsed >= 49 && status.PercentUsed <= 51);
            Assert.True(status.TrackingEnabled);
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task GetStatusAsync_DisabledTracking_StillReturnsStatus()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var svc = ServiceFactory.CreateBandwidthService(ctx, new BandwidthTrackingOptions { Enabled = false });
            var status = await svc.GetStatusAsync(acct.Id);
            Assert.False(status.TrackingEnabled);
        }
        finally { await scope.RollbackAsync(); }
    }

    // ============================================================
    // TrackUsageAsync
    // ============================================================

    [Fact]
    public async Task TrackUsageAsync_Disabled_ReturnsDummy()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var svc = ServiceFactory.CreateBandwidthService(ctx, new BandwidthTrackingOptions { Enabled = false });
            var usage = await svc.TrackUsageAsync(acct.Id, 1024);
            Assert.Equal(acct.Id, usage.MailAccountId);
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task TrackUsageAsync_AccumulatesBytes()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var svc = ServiceFactory.CreateBandwidthService(ctx);
            await svc.TrackUsageAsync(acct.Id, 1000, emailsProcessed: 2);
            await svc.TrackUsageAsync(acct.Id, 500, emailsProcessed: 1);
            var stored = await ctx.BandwidthUsages.AsNoTracking()
                .FirstAsync(u => u.MailAccountId == acct.Id && u.Date == DateTime.UtcNow.Date);
            Assert.Equal(1500, stored.BytesDownloaded);
            Assert.Equal(3, stored.EmailsProcessed);
        }
        finally { await scope.RollbackAsync(); }
    }

    // ============================================================
    // TrackUsageAndCheckLimitAsync
    // ============================================================

    [Fact]
    public async Task TrackUsageAndCheckLimitAsync_OverLimit_ReturnsTrue()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var opts = new BandwidthTrackingOptions { Enabled = true, DailyLimitMb = 1, PauseHoursOnLimit = 1 };
            await SeedUsageAsync(ctx, acct.Id, bytes: 0);
            var svc = ServiceFactory.CreateBandwidthService(ctx, opts);
            var (usage, reached) = await svc.TrackUsageAndCheckLimitAsync(acct.Id, 2 * 1024 * 1024);
            Assert.True(reached);
            Assert.True(usage.BytesDownloaded >= 2 * 1024 * 1024);
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task TrackUsageAndCheckLimitAsync_FlaggedNotReset_ReturnsTrue()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var opts = new BandwidthTrackingOptions { Enabled = true, DailyLimitMb = 1000 };
            await SeedUsageAsync(ctx, acct.Id, bytes: 100, limitReached: true,
                resetTime: DateTime.UtcNow.AddHours(1));
            var svc = ServiceFactory.CreateBandwidthService(ctx, opts);
            var (_, reached) = await svc.TrackUsageAndCheckLimitAsync(acct.Id, 50);
            Assert.True(reached);
        }
        finally { await scope.RollbackAsync(); }
    }

    // ============================================================
    // SetLimitReachedAsync / ClearLimitReachedAsync
    // ============================================================

    [Fact]
    public async Task SetLimitReachedAsync_SetsFlagAndResetTime()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var acct = await SeedAccountAsync(ctx);
            await SeedUsageAsync(ctx, acct.Id);
            var svc = ServiceFactory.CreateBandwidthService(ctx);
            var customReset = DateTime.UtcNow.AddHours(5);
            await svc.SetLimitReachedAsync(acct.Id, customReset);
            var stored = await ctx.BandwidthUsages.AsNoTracking()
                .FirstAsync(u => u.MailAccountId == acct.Id && u.Date == DateTime.UtcNow.Date);
            Assert.True(stored.LimitReached);
            Assert.NotNull(stored.LimitResetTime);
            Assert.InRange(stored.LimitResetTime!.Value, customReset.AddSeconds(-1), customReset.AddSeconds(1));
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task ClearLimitReachedAsync_Clears()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var acct = await SeedAccountAsync(ctx);
            await SeedUsageAsync(ctx, acct.Id, limitReached: true, resetTime: DateTime.UtcNow.AddHours(1));
            var svc = ServiceFactory.CreateBandwidthService(ctx);
            await svc.ClearLimitReachedAsync(acct.Id);
            var stored = await ctx.BandwidthUsages.AsNoTracking()
                .FirstAsync(u => u.MailAccountId == acct.Id && u.Date == DateTime.UtcNow.Date);
            Assert.False(stored.LimitReached);
            Assert.Null(stored.LimitResetTime);
        }
        finally { await scope.RollbackAsync(); }
    }

    // ============================================================
    // Checkpoints
    // ============================================================

    [Fact]
    public async Task GetOrCreateCheckpointAsync_CreatesThenReturns()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var svc = ServiceFactory.CreateBandwidthService(ctx);
            var c1 = await svc.GetOrCreateCheckpointAsync(acct.Id, "INBOX");
            Assert.Equal(acct.Id, c1.MailAccountId);
            Assert.Equal("INBOX", c1.FolderName);
            var c2 = await svc.GetOrCreateCheckpointAsync(acct.Id, "INBOX");
            Assert.Equal(c1.Id, c2.Id);
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task UpdateCheckpointAsync_AdvancesProcessedCount()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var svc = ServiceFactory.CreateBandwidthService(ctx);
            await svc.UpdateCheckpointAsync(acct.Id, "INBOX", DateTime.UtcNow, "msg1", 100);
            await svc.UpdateCheckpointAsync(acct.Id, "INBOX", DateTime.UtcNow, "msg2", 200);
            var cp = await ctx.SyncCheckpoints.AsNoTracking()
                .FirstAsync(c => c.MailAccountId == acct.Id && c.FolderName == "INBOX");
            Assert.Equal(2, cp.ProcessedCount);
            Assert.Equal("msg2", cp.LastMessageId);
            Assert.Equal(300, cp.BytesDownloaded);
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task MarkFolderCompletedAsync_SetsCompleted()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var svc = ServiceFactory.CreateBandwidthService(ctx);
            await svc.GetOrCreateCheckpointAsync(acct.Id, "Sent");
            await svc.MarkFolderCompletedAsync(acct.Id, "Sent");
            var cp = await ctx.SyncCheckpoints.AsNoTracking()
                .FirstAsync(c => c.MailAccountId == acct.Id && c.FolderName == "Sent");
            Assert.True(cp.IsCompleted);
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task ClearCheckpointsAsync_RemovesAll()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var svc = ServiceFactory.CreateBandwidthService(ctx);
            await svc.GetOrCreateCheckpointAsync(acct.Id, "INBOX");
            await svc.GetOrCreateCheckpointAsync(acct.Id, "Sent");
            await svc.ClearCheckpointsAsync(acct.Id);
            Assert.Empty(await ctx.SyncCheckpoints.AsNoTracking()
                .Where(c => c.MailAccountId == acct.Id).ToListAsync());
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task GetCheckpointsAsync_OrderedByFolderName()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var svc = ServiceFactory.CreateBandwidthService(ctx);
            await svc.GetOrCreateCheckpointAsync(acct.Id, "Sent");
            await svc.GetOrCreateCheckpointAsync(acct.Id, "INBOX");
            await svc.GetOrCreateCheckpointAsync(acct.Id, "Archive");
            var cps = await svc.GetCheckpointsAsync(acct.Id);
            Assert.Equal(3, cps.Count);
            for (int i = 1; i < cps.Count; i++)
                Assert.True(string.CompareOrdinal(cps[i - 1].FolderName, cps[i].FolderName) <= 0);
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task HasIncompleteCheckpointsAsync_True()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var svc = ServiceFactory.CreateBandwidthService(ctx);
            await svc.GetOrCreateCheckpointAsync(acct.Id, "INBOX"); // not completed
            Assert.True(await svc.HasIncompleteCheckpointsAsync(acct.Id));
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task HasIncompleteCheckpointsAsync_False()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var svc = ServiceFactory.CreateBandwidthService(ctx);
            await svc.GetOrCreateCheckpointAsync(acct.Id, "INBOX");
            await svc.MarkFolderCompletedAsync(acct.Id, "INBOX");
            Assert.False(await svc.HasIncompleteCheckpointsAsync(acct.Id));
        }
        finally { await scope.RollbackAsync(); }
    }

    // ============================================================
    // Cleanup
    // ============================================================

    [Fact]
    public async Task CleanupOldBandwidthRecordsAsync_RemovesOld()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var acct = await SeedAccountAsync(ctx);
            await SeedUsageAsync(ctx, acct.Id, date: DateTime.UtcNow.Date.AddDays(-10));
            var svc = ServiceFactory.CreateBandwidthService(ctx);
            var removed = await svc.CleanupOldBandwidthRecordsAsync(7);
            Assert.True(removed >= 1);
            Assert.Empty(await ctx.BandwidthUsages.AsNoTracking()
                .Where(u => u.MailAccountId == acct.Id && u.Date == DateTime.UtcNow.Date.AddDays(-10)).ToListAsync());
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task CleanupOldBandwidthRecordsAsync_Disabled_ReturnsZero()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var acct = await SeedAccountAsync(ctx);
            await SeedUsageAsync(ctx, acct.Id, date: DateTime.UtcNow.Date.AddDays(-10));
            var svc = ServiceFactory.CreateBandwidthService(ctx, new BandwidthTrackingOptions { Enabled = false });
            Assert.Equal(0, await svc.CleanupOldBandwidthRecordsAsync(7));
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task CleanupOldCheckpointsAsync_RemovesOnlyCompletedOld()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var svc = ServiceFactory.CreateBandwidthService(ctx);
            await svc.GetOrCreateCheckpointAsync(acct.Id, "INBOX");
            await svc.MarkFolderCompletedAsync(acct.Id, "INBOX");
            // Backdate the checkpoint.
            var cp = await ctx.SyncCheckpoints.FirstAsync(c => c.MailAccountId == acct.Id);
            cp.UpdatedAt = DateTime.UtcNow.AddDays(-31);
            await ctx.SaveChangesAsync();

            var removed = await svc.CleanupOldCheckpointsAsync(30);
            Assert.True(removed >= 1);
        }
        finally { await scope.RollbackAsync(); }
    }
}
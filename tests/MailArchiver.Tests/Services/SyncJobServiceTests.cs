using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services;
using MailArchiver.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MailArchiver.Tests.Services;

/// <summary>
/// Tests for <see cref="SyncJobService"/>. The in-memory job tracking is deterministic;
/// <see cref="SyncJobService.StartSyncAsync"/> and <see cref="SyncJobService.AcknowledgeJobFailures"/>
/// require a real <see cref="MailArchiverDbContext"/> (via <see cref="IServiceProvider"/> scopes)
/// and are run against the PostgreSQL Dev database with transactional rollback.
/// </summary>
[Collection(TestDbFixture.CollectionName)]
public class SyncJobServiceTests
{
    private readonly TestDbFixture _fixture;
    public SyncJobServiceTests(TestDbFixture fixture) => _fixture = fixture;

    private SyncJobService CreateService(MailArchiverDbContext sharedContext) =>
        new(NullLogger<SyncJobService>.Instance, ServiceFactory.BuildScopedProviderFor(sharedContext));

    private static async Task<MailAccount> SeedAccountAsync(MailArchiverDbContext ctx, ProviderType provider = ProviderType.IMAP)
    {
        var account = new MailAccount
        {
            Name = $"acct-{Guid.NewGuid():N}".Substring(0, 25),
            EmailAddress = $"{Guid.NewGuid():N}@test.local",
            Provider = provider,
            IsEnabled = true,
            LastSync = DateTime.UtcNow
        };
        ctx.MailAccounts.Add(account);
        await ctx.SaveChangesAsync();
        return account;
    }

    // ============================================================
    // StartSyncAsync
    // ============================================================

    [Fact]
    public async Task StartSyncAsync_NonexistentAccount_ReturnsNull()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var svc = CreateService(ctx);
            Assert.Null(await svc.StartSyncAsync(int.MaxValue - 1, "ghost"));
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task StartSyncAsync_ImportAccount_ReturnsNull()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx, ProviderType.IMPORT);
            var svc = CreateService(ctx);
            Assert.Null(await svc.StartSyncAsync(acct.Id, acct.Name));
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task StartSyncAsync_ExistingAccount_ReturnsJobId()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var svc = CreateService(ctx);
            var jobId = await svc.StartSyncAsync(acct.Id, acct.Name);
            Assert.NotNull(jobId);
            Assert.NotEmpty(jobId!);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task StartSyncAsync_SecondRunningJob_ThrowsInvalidOperationException()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var svc = CreateService(ctx);
            await svc.StartSyncAsync(acct.Id, acct.Name);
            await Assert.ThrowsAsync<InvalidOperationException>(() => svc.StartSyncAsync(acct.Id, acct.Name));
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task StartSync_LegacyMethod_ReturnsJobId()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var svc = CreateService(ctx);
            var jobId = svc.StartSync(acct.Id, acct.Name);
            Assert.NotEmpty(jobId);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task StartSync_LegacyMethod_Nonexistent_ThrowsInvalidOperationException()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var svc = CreateService(ctx);
            Assert.Throws<InvalidOperationException>(() => svc.StartSync(int.MaxValue - 1, "ghost"));
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    // ============================================================
    // GetJob / GetActiveJobs / GetAllJobs
    // ============================================================

    [Fact]
    public async Task GetJob_Existing_ReturnsJob()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var svc = CreateService(ctx);
            var jobId = await svc.StartSyncAsync(acct.Id, acct.Name);
            var job = svc.GetJob(jobId!);
            Assert.NotNull(job);
            Assert.Equal(SyncJobStatus.Running, job!.Status);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task GetJob_Unknown_ReturnsNull()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var svc = CreateService(ctx);
            Assert.Null(svc.GetJob("nonexistent"));
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task GetActiveJobs_OnlyRunning()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var a1 = await SeedAccountAsync(ctx);
            var a2 = await SeedAccountAsync(ctx);
            var svc = CreateService(ctx);
            var j1 = await svc.StartSyncAsync(a1.Id, a1.Name);
            var j2 = await svc.StartSyncAsync(a2.Id, a2.Name);
            svc.CompleteJob(j2!, true);

            var active = svc.GetActiveJobs();
            Assert.Single(active);
            Assert.Equal(j1, active[0].JobId);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task GetAllJobs_OrderedDescByStarted()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var a1 = await SeedAccountAsync(ctx);
            var a2 = await SeedAccountAsync(ctx);
            var svc = CreateService(ctx);
            var j1 = await svc.StartSyncAsync(a1.Id, a1.Name);
            await Task.Delay(15);
            var j2 = await svc.StartSyncAsync(a2.Id, a2.Name);

            var all = svc.GetAllJobs();
            Assert.Equal(2, all.Count);
            Assert.True(all[0].Started >= all[1].Started);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    // ============================================================
    // UpdateJobProgress
    // ============================================================

    [Fact]
    public async Task UpdateJobProgress_AppliesAction()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var svc = CreateService(ctx);
            var jobId = await svc.StartSyncAsync(acct.Id, acct.Name);
            svc.UpdateJobProgress(jobId!, j => { j.ProcessedEmails = 42; j.CurrentFolder = "INBOX"; });

            var job = svc.GetJob(jobId!);
            Assert.Equal(42, job!.ProcessedEmails);
            Assert.Equal("INBOX", job.CurrentFolder);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task UpdateJobProgress_UnknownJob_NoOp()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var svc = CreateService(ctx);
            svc.UpdateJobProgress("unknown", j => j.ProcessedEmails = 1);
            Assert.Null(svc.GetJob("unknown"));
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    // ============================================================
    // CompleteJob / CompleteJobRateLimited
    // ============================================================

    [Fact]
    public async Task CompleteJob_Success_SetsCompleted()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var svc = CreateService(ctx);
            var jobId = await svc.StartSyncAsync(acct.Id, acct.Name);
            svc.CompleteJob(jobId!, true);

            var job = svc.GetJob(jobId!);
            Assert.Equal(SyncJobStatus.Completed, job!.Status);
            Assert.NotNull(job.Completed);
            Assert.Null(job.ErrorMessage);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task CompleteJob_Failure_SetsFailedAndError()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var svc = CreateService(ctx);
            var jobId = await svc.StartSyncAsync(acct.Id, acct.Name);
            svc.CompleteJob(jobId!, false, "boom");

            var job = svc.GetJob(jobId!);
            Assert.Equal(SyncJobStatus.Failed, job!.Status);
            Assert.Equal("boom", job.ErrorMessage);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task CompleteJob_RemovesFromActive_AllowsRestart()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var svc = CreateService(ctx);
            var j1 = await svc.StartSyncAsync(acct.Id, acct.Name);
            svc.CompleteJob(j1!, true);
            var j2 = await svc.StartSyncAsync(acct.Id, acct.Name);
            Assert.NotEqual(j1, j2);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task CompleteJobRateLimited_SetsRateLimitedAndRemovesFromActive()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var svc = CreateService(ctx);
            var jobId = await svc.StartSyncAsync(acct.Id, acct.Name);
            svc.CompleteJobRateLimited(jobId!, "quota");

            var job = svc.GetJob(jobId!);
            Assert.Equal(SyncJobStatus.RateLimited, job!.Status);
            Assert.Equal("quota", job.ErrorMessage);
            // Should be removed from active, allowing restart
            var j2 = await svc.StartSyncAsync(acct.Id, acct.Name);
            Assert.NotNull(j2);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    // ============================================================
    // CancelJob / CancelJobsForAccount
    // ============================================================

    [Fact]
    public async Task CancelJob_Running_SetsCancelledAndCancelsToken()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var svc = CreateService(ctx);
            var jobId = await svc.StartSyncAsync(acct.Id, acct.Name);
            var job = svc.GetJob(jobId!);
            job!.CancellationTokenSource = new CancellationTokenSource();
            Assert.True(svc.CancelJob(jobId!));

            var updated = svc.GetJob(jobId!);
            Assert.Equal(SyncJobStatus.Cancelled, updated!.Status);
            Assert.True(updated.CancellationTokenSource!.IsCancellationRequested);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task CancelJob_NotRunning_ReturnsFalse()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var svc = CreateService(ctx);
            var jobId = await svc.StartSyncAsync(acct.Id, acct.Name);
            svc.CompleteJob(jobId!, true);
            Assert.False(svc.CancelJob(jobId!));
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task CancelJob_Unknown_ReturnsFalse()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var svc = CreateService(ctx);
            Assert.False(svc.CancelJob("unknown"));
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task CancelJobsForAccount_CancelsAllRunning()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var a1 = await SeedAccountAsync(ctx);
            var a2 = await SeedAccountAsync(ctx);
            var svc = CreateService(ctx);
            await svc.StartSyncAsync(a1.Id, a1.Name);
            await svc.StartSyncAsync(a2.Id, a2.Name);

            // Cancels the single running job for a1.
            Assert.True(svc.CancelJobsForAccount(a1.Id));
            // Second call: no running jobs for a1 anymore.
            Assert.False(svc.CancelJobsForAccount(a1.Id));
            // a2 still has a running job.
            Assert.True(svc.CancelJobsForAccount(a2.Id));
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task CancelJobsForAccount_NoneRunning_ReturnsFalse()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var svc = CreateService(ctx);
            Assert.False(svc.CancelJobsForAccount(acct.Id));
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    // ============================================================
    // AcknowledgeJobFailures
    // ============================================================

    [Fact]
    public async Task AcknowledgeJobFailures_UnknownJob_ReturnsFalse()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var svc = CreateService(ctx);
            Assert.False(svc.AcknowledgeJobFailures("unknown"));
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task AcknowledgeJobFailures_NotCompleted_ReturnsFalse()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var svc = CreateService(ctx);
            var jobId = await svc.StartSyncAsync(acct.Id, acct.Name);
            Assert.False(svc.AcknowledgeJobFailures(jobId!));
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task AcknowledgeJobFailures_NoFailedEmails_ReturnsFalse()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var svc = CreateService(ctx);
            var jobId = await svc.StartSyncAsync(acct.Id, acct.Name);
            svc.CompleteJob(jobId!, true);
            Assert.False(svc.AcknowledgeJobFailures(jobId!));
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task AcknowledgeJobFailures_AlreadyAcknowledged_ReturnsFalse()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var originalLastSync = acct.LastSync;
            var svc = CreateService(ctx);
            var jobId = await svc.StartSyncAsync(acct.Id, acct.Name, acct.LastSync);
            svc.UpdateJobProgress(jobId!, j => { j.FailedEmails = 3; });
            svc.CompleteJob(jobId!, true);
            Assert.True(svc.AcknowledgeJobFailures(jobId!));
            Assert.False(svc.AcknowledgeJobFailures(jobId!));
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task AcknowledgeJobFailures_AdvancesLastSyncAndSetsFlag()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var oldLastSync = DateTime.UtcNow.AddDays(-5);
            acct.LastSync = oldLastSync;
            await ctx.SaveChangesAsync();

            var svc = CreateService(ctx);
            var jobId = await svc.StartSyncAsync(acct.Id, acct.Name, acct.LastSync);
            svc.UpdateJobProgress(jobId!, j => { j.FailedEmails = 5; });
            svc.CompleteJob(jobId!, true);

            Assert.True(svc.AcknowledgeJobFailures(jobId!));

            var refreshed = await ctx.MailAccounts.AsNoTracking().FirstAsync(a => a.Id == acct.Id);
            Assert.True(refreshed.LastSync > oldLastSync);
            Assert.True(svc.GetJob(jobId!)!.FailuresAcknowledged);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    // ============================================================
    // CleanupOldJobs
    // ============================================================

    [Fact]
    public async Task CleanupOldJobs_RemovesOlderThan24h()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var svc = CreateService(ctx);
            var jobId = await svc.StartSyncAsync(acct.Id, acct.Name);
            svc.CompleteJob(jobId!, true);
            // Manually backdate the completion time past 24h.
            svc.UpdateJobProgress(jobId!, j => j.Completed = DateTime.UtcNow.AddHours(-25));
            svc.CleanupOldJobs();

            Assert.Null(svc.GetJob(jobId!));
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task CleanupOldJobs_KeepsRecentCompleted()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var svc = CreateService(ctx);
            var jobId = await svc.StartSyncAsync(acct.Id, acct.Name);
            svc.CompleteJob(jobId!, true);
            svc.CleanupOldJobs();
            Assert.NotNull(svc.GetJob(jobId!));
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    /// <summary>
    /// Removes all test rows for accounts created in the given context.
    /// </summary>
    private static async Task CleanupTestAccountAsync(MailArchiverDbContext ctx)
    {
        var accountIds = await ctx.MailAccounts.AsNoTracking()
            .Where(a => a.EmailAddress.EndsWith("@test.local"))
            .Select(a => a.Id)
            .ToListAsync();

        if (accountIds.Count == 0) return;

        var emails = await ctx.ArchivedEmails.Where(e => accountIds.Contains(e.MailAccountId)).ToListAsync();
        ctx.ArchivedEmails.RemoveRange(emails);

        var umas = await ctx.UserMailAccounts.Where(uma => accountIds.Contains(uma.MailAccountId)).ToListAsync();
        ctx.UserMailAccounts.RemoveRange(umas);

        var accts = await ctx.MailAccounts.Where(a => accountIds.Contains(a.Id)).ToListAsync();
        ctx.MailAccounts.RemoveRange(accts);

        try { await ctx.SaveChangesAsync(); }
        catch { /* best-effort cleanup */ }
    }
}
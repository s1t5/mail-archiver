using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services;
using MailArchiver.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MailArchiver.Tests.Services;

/// <summary>
/// Restart and cancellation lifecycle of the audit export service: the queue is in
/// memory only, so the startup sweep is what keeps persisted rows from sitting in
/// Queued/Running forever, and cancel must always hit the job it was asked to cancel.
/// </summary>
[Collection(TestDbFixture.CollectionName)]
public class AuditExportServiceLifecycleTests
{
    private readonly TestDbFixture _fixture;
    private readonly string _outputDir;

    public AuditExportServiceLifecycleTests(TestDbFixture fixture)
    {
        _fixture = fixture;
        _outputDir = Path.Combine(Path.GetTempPath(), "audit-export-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_outputDir);
    }

    private sealed class TestHostingEnvironment : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "MailArchiver.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Development";
    }

    private sealed class CapturingLogger : Microsoft.Extensions.Logging.ILogger<AuditExportService>
    {
        private readonly List<string> _messages;
        public CapturingLogger(List<string> messages) => _messages = messages;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => _messages.Add($"[{logLevel}] {formatter(state, exception)} {exception?.Message ?? ""}");
    }

    private (AuditExportService Service, List<string> Logs) CreateService(MailArchiverDbContext sharedContext)
    {
        var logs = new List<string>();
        var services = new ServiceCollection();
        // The service runs its loop on its own connection: the background loop and the
        // test thread both query the DB concurrently, which a single shared Npgsql
        // connection does not allow (one command per connection at a time).
        services.AddDbContext<MailArchiverDbContext>(options => options
            .UseNpgsql(_fixture.ConnectionString)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));
        services.AddScoped<IAccessLogService, AccessLogService>();
        services.AddSingleton<IOptions<TimeZoneOptions>>(_ =>
            Options.Create(new TimeZoneOptions { DisplayTimeZoneId = "Etc/UCT" }));
        var provider = services.BuildServiceProvider();

        var service = new AuditExportService(
            provider,
            new CapturingLogger(logs),
            new TestHostingEnvironment(),
            Options.Create(new AuditExportOptions
            {
                DataSupplierName = "Testfirma GmbH",
                DataSupplierLocation = "Teststadt",
                Comment = "Kommentar",
                OutputDirectory = _outputDir
            }));
        return (service, logs);
    }

    /// <summary>
    /// Polls the database until the job reaches one of the given statuses. Seeded rows
    /// have no in-memory completion signal, and the service runs against a separate
    /// connection — polling is the only reliable observation here.
    /// </summary>
    private async Task<AuditExportJob> WaitForStatusAsync(Guid jobId, TimeSpan timeout,
        params AuditExportJobStatus[] statuses)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            using var context = _fixture.CreateContext();
            var job = await context.AuditExportJobs.AsNoTracking()
                .FirstOrDefaultAsync(j => j.Id == jobId);
            if (job != null && statuses.Contains(job.Status))
            {
                return job;
            }
            await Task.Delay(100);
        }
        using var failContext = _fixture.CreateContext();
        return await failContext.AuditExportJobs.AsNoTracking().SingleAsync(j => j.Id == jobId);
    }

    private async Task<AuditExportJob> SeedJobAsync(AuditExportJobStatus status, string? outputFilePath = null)
    {
        using var context = _fixture.CreateContext();
        var job = new AuditExportJob
        {
            Id = Guid.NewGuid(),
            Username = "lifecycle-admin",
            FromDate = DateTime.UtcNow.AddDays(-30),
            ToDate = DateTime.UtcNow.AddDays(1),
            Status = status,
            OutputFilePath = outputFilePath
        };
        context.AuditExportJobs.Add(job);
        await context.SaveChangesAsync();
        return job;
    }

    private async Task CleanupJobsAsync(params Guid[] jobIds)
    {
        using var cleanupContext = _fixture.CreateContext();
        var rows = cleanupContext.AuditExportJobs.Where(j => jobIds.Contains(j.Id)).ToList();
        foreach (var row in rows)
        {
            if (row.OutputFilePath != null && File.Exists(row.OutputFilePath))
            {
                File.Delete(row.OutputFilePath);
            }
            cleanupContext.AuditExportJobs.Remove(row);
        }
        var logRows = cleanupContext.AccessLogs
            .Where(l => l.Type == AccessLogType.AuditExport && l.Username == "lifecycle-admin")
            .ToList();
        cleanupContext.AccessLogs.RemoveRange(logRows);
        await cleanupContext.SaveChangesAsync();
    }

    [Fact]
    public async Task StartAsync_MarksInterruptedRunningAsFailed_AndReEnqueuesQueued()
    {
        // A partial ZIP from the interrupted run; the sweep must delete it
        var partialZipPath = Path.Combine(_outputDir, $"audit-export-interrupted-{Guid.NewGuid():N}.zip");
        await File.WriteAllTextAsync(partialZipPath, "partial");
        var runningJob = await SeedJobAsync(AuditExportJobStatus.Running, partialZipPath);
        var queuedJob = await SeedJobAsync(AuditExportJobStatus.Queued);

        try
        {
            using var setupContext = _fixture.CreateContext();
            var (service, logs) = CreateService(setupContext);

            using var hostCts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            await service.StartAsync(hostCts.Token);

            // The re-enqueued Queued job runs through to a final state; the Running one is
            // closed immediately by the sweep.
            var queuedRow = await WaitForStatusAsync(queuedJob.Id, TimeSpan.FromSeconds(90),
                AuditExportJobStatus.Completed, AuditExportJobStatus.Failed, AuditExportJobStatus.Cancelled);
            await service.StopAsync(CancellationToken.None);

            var runningRow = await WaitForStatusAsync(runningJob.Id, TimeSpan.FromSeconds(10),
                AuditExportJobStatus.Completed, AuditExportJobStatus.Failed, AuditExportJobStatus.Cancelled);

            Assert.Equal(AuditExportJobStatus.Failed, runningRow.Status);
            Assert.NotNull(runningRow.Completed);
            Assert.Contains("restart", runningRow.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(partialZipPath), "Partial ZIP of the interrupted run must be deleted");

            // Final state must be terminal, not stuck Queued
            Assert.True(queuedRow.Status is AuditExportJobStatus.Completed or AuditExportJobStatus.Failed,
                $"Re-enqueued job should have run to a terminal state, got {queuedRow.Status} ({queuedRow.ErrorMessage}), logs: {string.Join(" | ", logs)}");
            Assert.True(logs.Any(l => l.Contains("restart recovery", StringComparison.OrdinalIgnoreCase)),
                "Expected a restart recovery log line");
        }
        finally
        {
            await CleanupJobsAsync(runningJob.Id, queuedJob.Id);
        }
    }

    [Fact]
    public async Task CancelWhileQueued_DoesNotLeakCompletionSignal()
    {
        using var setupContext = _fixture.CreateContext();
        var (service, logs) = CreateService(setupContext);

        var job = await service.StartJobAsync(new AuditExportRequest
        {
            FromDate = DateTime.UtcNow.AddDays(-30),
            ToDate = DateTime.UtcNow.AddDays(1)
        }, "lifecycle-admin");

        try
        {
            // Cancel before the loop dequeues the job (the service loop is not started yet)
            var cancelled = await service.CancelJobAsync(job.Id);
            Assert.True(cancelled);

            // Start the loop: it dequeues the cancelled row, takes the early return, and
            // (with the fix) still runs the signal cleanup instead of leaking the entry.
            using var hostCts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            await service.StartAsync(hostCts.Token);

            // With the leak this throws TimeoutException because the signal never fires;
            // with the fix the early return's finally completes it immediately.
            await service.WaitForJobAsync(job.Id, TimeSpan.FromSeconds(30));
            await service.StopAsync(CancellationToken.None);

            var row = await WaitForStatusAsync(job.Id, TimeSpan.FromSeconds(10),
                AuditExportJobStatus.Cancelled);
            Assert.Equal(AuditExportJobStatus.Cancelled, row.Status);
        }
        finally
        {
            await CleanupJobsAsync(job.Id);
        }
    }

    [Fact]
    public async Task CancelStaleRunningRow_CancelsThatRow_AndSparesTheLiveJob()
    {
        using var setupContext = _fixture.CreateContext();
        var (service, logs) = CreateService(setupContext);

        // Start the loop. The startup sweep runs concurrently to StartAsync returning;
        // a sentinel job proves it is through: the loop only processes it afterwards.
        using var hostCts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await service.StartAsync(hostCts.Token);

        var sentinel = await service.StartJobAsync(new AuditExportRequest
        {
            // Narrow empty range so the sentinel export itself is fast
            FromDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ToDate = new DateTime(2020, 1, 1, 23, 0, 0, DateTimeKind.Utc)
        }, "lifecycle-admin");
        await service.WaitForJobAsync(sentinel.Id, TimeSpan.FromSeconds(90));

        // NOW the sweep is done: a Running row seeded from here on simulates one the
        // sweep missed (it is best-effort; a failed recovery query leaves such rows).
        var staleJob = await SeedJobAsync(AuditExportJobStatus.Running);

        // Start a real job that will actually run
        var liveJob = await service.StartJobAsync(new AuditExportRequest
        {
            FromDate = DateTime.UtcNow.AddDays(-30),
            ToDate = DateTime.UtcNow.AddDays(1)
        }, "lifecycle-admin");

        try
        {
            // Give the loop a moment to pick up the live job so it sits in Running
            await Task.Delay(500);

            // Cancel the stale row while the live job is (or is about to be) running.
            // Before the fix this fired the single shared CTS and killed the live job.
            var cancelledStale = await service.CancelJobAsync(staleJob.Id);
            Assert.True(cancelledStale);

            await service.WaitForJobAsync(liveJob.Id, TimeSpan.FromSeconds(90));
            await service.StopAsync(CancellationToken.None);

            var staleRow = await WaitForStatusAsync(staleJob.Id, TimeSpan.FromSeconds(10),
                AuditExportJobStatus.Cancelled);
            Assert.Equal(AuditExportJobStatus.Cancelled, staleRow.Status);
            Assert.NotNull(staleRow.Completed);

            var liveRow = await WaitForStatusAsync(liveJob.Id, TimeSpan.FromSeconds(10),
                AuditExportJobStatus.Completed, AuditExportJobStatus.Failed);
            Assert.True(liveRow.Status is AuditExportJobStatus.Completed or AuditExportJobStatus.Failed,
                $"Live job must run to a terminal state untouched, got {liveRow.Status} ({liveRow.ErrorMessage}), logs: {string.Join(" | ", logs)}");
        }
        finally
        {
            await CleanupJobsAsync(staleJob.Id, liveJob.Id, sentinel.Id);
        }
    }
}
using MailArchiver.Models;
using MailArchiver.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Reflection;
using Xunit;

namespace MailArchiver.Tests.Services;

/// <summary>
/// The finally block of ProcessJob removes and disposes the CTS, but CancelJob can still
/// read it from the dictionary in the race window. Cancelling an already-disposed source
/// throws ObjectDisposedException, which surfaced as HTTP 500 (M6).
/// </summary>
public class BatchRestoreServiceCancelTests
{
    private static BatchRestoreService CreateService()
        => new(serviceProvider: null!,                     // never touched by CancelJob
               NullLogger<BatchRestoreService>.Instance,
               Options.Create(new BatchOperationOptions()),
               Options.Create(new OffloadOptions()));

    [Fact]
    public void CancelJob_AfterCtsDisposed_DoesNotThrow()
    {
        var service = CreateService();
        var job = new BatchRestoreJob();
        service.QueueJob(job);
        job.Status = BatchRestoreJobStatus.Running;

        // Inject an already-disposed CTS, the state the race window leaves behind.
        var field = typeof(BatchRestoreService)
            .GetField("_jobCancellations",
                BindingFlags.NonPublic | BindingFlags.Instance)!;
        var dict = (ConcurrentDictionary<string, CancellationTokenSource>)field.GetValue(service)!;
        var cts = new CancellationTokenSource();
        dict[job.JobId] = cts;
        cts.Dispose();

        // Formerly ObjectDisposedException → HTTP 500. Now: still success/no-op.
        var result = service.CancelJob(job.JobId);
        Assert.True(result);
    }

    [Fact]
    public void CancelJob_RunningJobWithoutCts_DoesNotThrow()
    {
        var service = CreateService();
        var job = new BatchRestoreJob();
        service.QueueJob(job);
        job.Status = BatchRestoreJobStatus.Running;

        var result = service.CancelJob(job.JobId);
        Assert.True(result);
    }

    [Fact]
    public void CancelJob_QueuedJob_CancelsWithoutCts()
    {
        var service = CreateService();
        var job = new BatchRestoreJob();
        service.QueueJob(job);

        var result = service.CancelJob(job.JobId);
        Assert.True(result);
        Assert.Equal(BatchRestoreJobStatus.Cancelled, job.Status);
    }

    [Fact]
    public void CancelJob_UnknownJob_ReturnsFalse()
    {
        var service = CreateService();
        Assert.False(service.CancelJob("does-not-exist"));
    }
}
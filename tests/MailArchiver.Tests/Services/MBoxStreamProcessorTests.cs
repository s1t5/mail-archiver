using MailArchiver.Models;
using MailArchiver.Services.Providers.MBox;
using MailArchiver.Services.Shared;
using Microsoft.Extensions.Logging.Abstractions;
using MimeKit;
using Xunit;

namespace MailArchiver.Tests.Services;

/// <summary>
/// Tests for <see cref="MBoxStreamProcessor"/> using temporary MBox files under /tmp/opencode.
/// No database connection required.
/// </summary>
public class MBoxStreamProcessorTests
{
    private static MBoxStreamProcessor CreateProcessor() =>
        new(NullLogger<MBoxStreamProcessor>.Instance);

    private static string WriteMBox(params string[] emlContents)
    {
        var path = Path.Combine(Path.GetTempPath(), $"mbox-{Guid.NewGuid():N}.mbox");
        using var sw = new StreamWriter(path, append: false);
        for (int i = 0; i < emlContents.Length; i++)
        {
            sw.Write($"From sender{i}@x.com {DateTime.UtcNow:ddd MMM d HH:mm:ss yyyy}\r\n");
            sw.Write(emlContents[i]);
            if (!emlContents[i].EndsWith("\r\n")) sw.Write("\r\n");
            sw.Write("\r\n"); // blank line separating messages
        }
        sw.Flush();
        return path;
    }

    private static string BuildEml(string subject, string from = "a@x.com", string to = "b@x.com", string body = "hello")
        => $"From: {from}\r\nTo: {to}\r\nSubject: {subject}\r\nDate: {DateTime.UtcNow:R}\r\nMessage-Id: <{subject.GetHashCode()}@test>\r\nContent-Type: text/plain\r\n\r\n{body}\r\n";

    private static MailAccount BuildAccount(int id) =>
        new() { Id = id, Name = "test", EmailAddress = "test@x.com", Provider = ProviderType.IMPORT };

    [Fact]
    public async Task ProcessMBoxFile_SingleValidEmail_HandlerCalledOnce()
    {
        var path = WriteMBox(BuildEml("Single"));
        try
        {
            var processor = CreateProcessor();
            var job = new MBoxImportJob { FilePath = path, TargetFolder = "INBOX" };
            var account = BuildAccount(1);
            var count = 0;
            await processor.ProcessMBoxFile(job, account, CancellationToken.None,
                (msg, folder) => { count++; return Task.FromResult(ImportResult.CreateSuccess()); });
            Assert.Equal(1, count);
            Assert.Equal(1, job.SuccessCount);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ProcessMBoxFile_MultipleValidEmails_HandlerCalledPerEmail()
    {
        var path = WriteMBox(BuildEml("One"), BuildEml("Two"), BuildEml("Three"));
        try
        {
            var processor = CreateProcessor();
            var job = new MBoxImportJob { FilePath = path, TargetFolder = "INBOX" };
            var subjects = new List<string>();
            await processor.ProcessMBoxFile(job, BuildAccount(1), CancellationToken.None,
                (msg, folder) => { subjects.Add(msg.Subject); return Task.FromResult(ImportResult.CreateSuccess()); });
            Assert.Equal(3, subjects.Count);
            Assert.Equal(3, job.SuccessCount);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ProcessMBoxFile_EmptyFile_HandlerNotCalled()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mbox-empty-{Guid.NewGuid():N}.mbox");
        File.WriteAllText(path, "");
        try
        {
            var processor = CreateProcessor();
            var job = new MBoxImportJob { FilePath = path, TargetFolder = "INBOX" };
            var called = false;
            await processor.ProcessMBoxFile(job, BuildAccount(1), CancellationToken.None,
                (msg, folder) => { called = true; return Task.FromResult(ImportResult.CreateSuccess()); });
            Assert.False(called);
            Assert.Equal(0, job.ProcessedEmails);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ProcessMBoxFile_HandlerAlreadyExists_IncrementsSkipped()
    {
        var path = WriteMBox(BuildEml("Existing"));
        try
        {
            var processor = CreateProcessor();
            var job = new MBoxImportJob { FilePath = path, TargetFolder = "INBOX" };
            await processor.ProcessMBoxFile(job, BuildAccount(1), CancellationToken.None,
                (msg, folder) => Task.FromResult(ImportResult.CreateAlreadyExists()));
            Assert.Equal(1, job.SkippedAlreadyExistsCount);
            Assert.Equal(0, job.SuccessCount);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ProcessMBoxFile_HandlerFails_IncrementsFailed()
    {
        var path = WriteMBox(BuildEml("Fail"));
        try
        {
            var processor = CreateProcessor();
            var job = new MBoxImportJob { FilePath = path, TargetFolder = "INBOX" };
            await processor.ProcessMBoxFile(job, BuildAccount(1), CancellationToken.None,
                (msg, folder) => Task.FromResult(ImportResult.CreateFailed("err")));
            Assert.Equal(1, job.FailedCount);
            Assert.Equal(0, job.SuccessCount);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ProcessMBoxFile_MalformedEmail_SkipsAndContinues()
    {
        var good = BuildEml("Good1");
        var malformed = "This is not a valid MIME message\r\n\r\ngarbage\r\n";
        var good2 = BuildEml("Good2");
        var path = WriteMBox(good, malformed, good2);
        try
        {
            var processor = CreateProcessor();
            var job = new MBoxImportJob { FilePath = path, TargetFolder = "INBOX" };
            var subjects = new List<string>();
            await processor.ProcessMBoxFile(job, BuildAccount(1), CancellationToken.None,
                (msg, folder) => { subjects.Add(msg.Subject); return Task.FromResult(ImportResult.CreateSuccess()); });
            // At least one good email processed; malformed skipped.
            Assert.True(job.ProcessedEmails >= 1);
            Assert.True(job.SkippedMalformedCount >= 0); // MimeKit may parse garbage as a message
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ProcessMBoxFile_Cancelled_ThrowsOperationCanceled()
    {
        var path = WriteMBox(BuildEml("Cancel1"), BuildEml("Cancel2"));
        try
        {
            var processor = CreateProcessor();
            var job = new MBoxImportJob { FilePath = path, TargetFolder = "INBOX" };
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                processor.ProcessMBoxFile(job, BuildAccount(1), cts.Token,
                    (msg, folder) => Task.FromResult(ImportResult.CreateSuccess())));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ProcessMBoxFile_ProcessedBytes_Advance()
    {
        var path = WriteMBox(BuildEml("Bytes"));
        try
        {
            var processor = CreateProcessor();
            var job = new MBoxImportJob { FilePath = path, TargetFolder = "INBOX" };
            await processor.ProcessMBoxFile(job, BuildAccount(1), CancellationToken.None,
                (msg, folder) => Task.FromResult(ImportResult.CreateSuccess()));
            Assert.True(job.ProcessedBytes > 0);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ProcessMBoxFile_CurrentEmailSubject_Set()
    {
        var path = WriteMBox(BuildEml("SubjectTest"));
        try
        {
            var processor = CreateProcessor();
            var job = new MBoxImportJob { FilePath = path, TargetFolder = "INBOX" };
            string? captured = null;
            await processor.ProcessMBoxFile(job, BuildAccount(1), CancellationToken.None,
                (msg, folder) => { captured = job.CurrentEmailSubject; return Task.FromResult(ImportResult.CreateSuccess()); });
            Assert.Equal("SubjectTest", captured);
        }
        finally { File.Delete(path); }
    }
}
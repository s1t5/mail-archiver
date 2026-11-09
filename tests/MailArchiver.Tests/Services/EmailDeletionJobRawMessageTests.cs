using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services;
using MailArchiver.Services.Shared;
using MailArchiver.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace MailArchiver.Tests.Services;

/// <summary>
/// Wires the real <see cref="EmailDeletionService"/> to a filesystem raw message store and
/// checks that a finished job removes the stored original of every deleted email. The store
/// itself is covered by RawMessageDeletionTests; this covers the job's call into it, which a
/// service built without an <see cref="IRawMessageStore"/> silently skips.
/// </summary>
[Collection(TestDbFixture.CollectionName)]
public class EmailDeletionJobRawMessageTests : IDisposable
{
    private readonly TestDbFixture _fixture;
    private readonly string _basePath;

    public EmailDeletionJobRawMessageTests(TestDbFixture fixture)
    {
        _fixture = fixture;
        _basePath = Path.Combine(Path.GetTempPath(), "jobraw-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_basePath);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_basePath))
            {
                Directory.Delete(_basePath, recursive: true);
            }
        }
        catch
        {
            // A leftover temp directory must not fail the test run.
        }
    }

    private RawMessageOptions BuildOptions() => new()
    {
        Enabled = true,
        Storage = RawMessageStorage.FileSystem,
        MaxMessageSizeMb = 50,
        FileSystem = new RawMessageFileSystemOptions
        {
            BasePath = _basePath,
            ShardDepth = 2,
            VerifyChecksumOnRead = true
        }
    };

    private static byte[] SampleMessage(string marker)
        => System.Text.Encoding.ASCII.GetBytes(
            "From: a@x.com\r\nTo: b@y.com\r\nSubject: " + marker + "\r\n\r\nBody " + marker + "\r\n");

    private static MailAccount NewAccount() => new()
    {
        Name = "jobraw-" + Guid.NewGuid().ToString("N")[..8],
        EmailAddress = $"{Guid.NewGuid():N}@jobraw.local",
        Username = "jobraw@jobraw.local",
        Password = "x",
        Provider = ProviderType.IMPORT,
        IsEnabled = false,
        LastSync = DateTime.UtcNow
    };

    private static ArchivedEmail NewEmail(int accountId, string subject) => new()
    {
        MailAccountId = accountId,
        MessageId = $"<{Guid.NewGuid():N}@jobraw.local>",
        Subject = subject,
        Body = "body",
        HtmlBody = string.Empty,
        From = "sender@jobraw.local",
        To = "rcpt@jobraw.local",
        Cc = string.Empty,
        Bcc = string.Empty,
        FolderName = "INBOX",
        SentDate = DateTime.UtcNow.AddDays(-1),
        ReceivedDate = DateTime.UtcNow
    };

    private static async Task<(ArchivedEmail Email, string Path)> SeedWithOriginalAsync(
        MailArchiverDbContext ctx,
        RawMessageOptions options,
        RawMessagePathResolver resolver,
        int accountId,
        string subject)
    {
        var email = NewEmail(accountId, subject);
        ctx.ArchivedEmails.Add(email);
        await ctx.SaveChangesAsync();

        var capture = new RawMessageCapture(SampleMessage(subject), RawMessageSource.Imap);
        var store = new FileSystemRawMessageStore(
            ctx, Options.Create(options), resolver, NullLogger<FileSystemRawMessageStore>.Instance);
        var row = store.CreateRow(email.Id, capture);
        await store.PersistAsync(row!, capture);

        email.RawMessage = row;
        await ctx.SaveChangesAsync();

        return (email, resolver.GetAbsolutePath(email.Id));
    }

    private static EmailDeletionService CreateService(
        MailArchiverDbContext ctx,
        RawMessageOptions options,
        RawMessagePathResolver resolver,
        IOptions<DeletionPolicyOptions>? policy = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(ctx);
        services.AddScoped<IAccessLogService, AccessLogService>();
        services.AddSingleton(policy ?? Options.Create(new DeletionPolicyOptions()));
        services.AddSingleton<IRawMessageStore>(
            new FileSystemRawMessageStore(
                ctx, Options.Create(options), resolver, NullLogger<FileSystemRawMessageStore>.Instance));

        var provider = services.BuildServiceProvider();
        return new EmailDeletionService(
            NullLogger<EmailDeletionService>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>(),
            batchSize: 10);
    }

    /// <summary>Queues first, then starts the loop, so the queue cannot race the test's setup.</summary>
    private static async Task<EmailDeletionJob> RunJobAsync(EmailDeletionService service, EmailDeletionJob job)
    {
        service.QueueJob(job);
        await service.StartAsync(CancellationToken.None);
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(60);
            while (!job.IsCompleted && DateTime.UtcNow < deadline)
            {
                await Task.Delay(50);
            }
            return job;
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task IdJob_DeletesTheStoredOriginalOfTheDeletedEmail()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        var options = BuildOptions();
        var resolver = new RawMessagePathResolver(options.FileSystem, _basePath);

        var account = NewAccount();
        ctx.MailAccounts.Add(account);
        await ctx.SaveChangesAsync();

        var (email, path) = await SeedWithOriginalAsync(ctx, options, resolver, account.Id, "invoice one");
        var (kept, keptPath) = await SeedWithOriginalAsync(ctx, options, resolver, account.Id, "newsletter");
        Assert.True(File.Exists(path));

        var service = CreateService(ctx, options, resolver);
        var job = await RunJobAsync(service, new EmailDeletionJob { EmailIds = new List<int> { email.Id } });

        Assert.Equal(EmailDeletionJobStatus.Completed, job.Status);
        Assert.False(File.Exists(path));
        Assert.False(await ctx.ArchivedEmails.AsNoTracking().AnyAsync(e => e.Id == email.Id));
        Assert.False(await ctx.RawMessages.AsNoTracking().AnyAsync(r => r.ArchivedEmailId == email.Id));

        // The untouched email keeps its original.
        Assert.True(File.Exists(keptPath));
        Assert.True(await ctx.ArchivedEmails.AsNoTracking().AnyAsync(e => e.Id == kept.Id));

        await scope.RollbackAsync();
    }

    [Fact]
    public async Task FilterJob_DeletesOnlyTheOriginalOfAMatchingEmail()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        var options = BuildOptions();
        var resolver = new RawMessagePathResolver(options.FileSystem, _basePath);

        var account = NewAccount();
        ctx.MailAccounts.Add(account);
        await ctx.SaveChangesAsync();

        var (match, matchPath) = await SeedWithOriginalAsync(ctx, options, resolver, account.Id, "invoice two");
        var (other, otherPath) = await SeedWithOriginalAsync(ctx, options, resolver, account.Id, "newsletter");

        var service = CreateService(ctx, options, resolver);
        var job = await RunJobAsync(service, new EmailDeletionJob
        {
            Filter = new EmailDeletionFilter { AccountId = account.Id, SearchTerm = "invoice" }
        });

        Assert.Equal(EmailDeletionJobStatus.Completed, job.Status);
        Assert.Equal(1, job.DeletedEmails);
        Assert.False(File.Exists(matchPath));
        Assert.False(await ctx.ArchivedEmails.AsNoTracking().AnyAsync(e => e.Id == match.Id));

        Assert.True(File.Exists(otherPath));
        Assert.True(await ctx.ArchivedEmails.AsNoTracking().AnyAsync(e => e.Id == other.Id));

        await scope.RollbackAsync();
    }

    [Fact]
    public async Task FilterJob_AboveTheCapAtStart_FailsWithoutDeletingAnything()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        var options = BuildOptions();
        var resolver = new RawMessagePathResolver(options.FileSystem, _basePath);

        var account = NewAccount();
        ctx.MailAccounts.Add(account);
        await ctx.SaveChangesAsync();

        var (first, firstPath) = await SeedWithOriginalAsync(ctx, options, resolver, account.Id, "invoice one");
        var (second, secondPath) = await SeedWithOriginalAsync(ctx, options, resolver, account.Id, "invoice two");

        // The controller would reject this before queueing; the job must not trust that.
        var service = CreateService(ctx, options, resolver,
            Options.Create(new DeletionPolicyOptions { MaxBulkDeleteEmails = 1 }));
        var job = await RunJobAsync(service, new EmailDeletionJob
        {
            Filter = new EmailDeletionFilter { AccountId = account.Id, SearchTerm = "invoice" }
        });

        Assert.Equal(EmailDeletionJobStatus.Failed, job.Status);
        Assert.Contains("MaxBulkDeleteEmails", job.ErrorMessage);
        Assert.True(File.Exists(firstPath));
        Assert.True(File.Exists(secondPath));
        Assert.True(await ctx.ArchivedEmails.AsNoTracking().AnyAsync(e => e.Id == first.Id));
        Assert.True(await ctx.ArchivedEmails.AsNoTracking().AnyAsync(e => e.Id == second.Id));

        await scope.RollbackAsync();
    }

    [Fact]
    public async Task Job_WhenDeletionIsDisabledAfterQueueing_FailsWithoutDeletingAnything()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        var options = BuildOptions();
        var resolver = new RawMessagePathResolver(options.FileSystem, _basePath);

        var account = NewAccount();
        ctx.MailAccounts.Add(account);
        await ctx.SaveChangesAsync();

        var (email, path) = await SeedWithOriginalAsync(ctx, options, resolver, account.Id, "invoice one");

        var policy = new MutableOptions<DeletionPolicyOptions>(
            new DeletionPolicyOptions { DeletionAllowed = true });
        var service = CreateService(ctx, options, resolver, policy);

        var job = new EmailDeletionJob { EmailIds = new List<int> { email.Id } };
        service.QueueJob(job);

        // The administrator disables deletion while the job waits in the queue.
        policy.Value.DeletionAllowed = false;
        await service.StartAsync(CancellationToken.None);
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(60);
            while (!job.IsCompleted && DateTime.UtcNow < deadline)
            {
                await Task.Delay(50);
            }
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }

        Assert.Equal(EmailDeletionJobStatus.Failed, job.Status);
        Assert.Contains("DeletionAllowed", job.ErrorMessage);
        Assert.True(File.Exists(path));
        Assert.True(await ctx.ArchivedEmails.AsNoTracking().AnyAsync(e => e.Id == email.Id));

        await scope.RollbackAsync();
    }

    /// <summary>An options snapshot whose value the test can change between queueing and start.</summary>
    private sealed class MutableOptions<T> : IOptions<T> where T : class
    {
        public T Value { get; set; }
        public MutableOptions(T value) => Value = value;
    }
}

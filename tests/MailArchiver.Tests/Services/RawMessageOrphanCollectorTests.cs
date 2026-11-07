using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services.Shared;
using MailArchiver.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace MailArchiver.Tests.Services;

/// <summary>
/// Tests for the orphan collector, the net under every delete path: a file that no row points
/// at is removed, a referenced one is kept, and a file written moments ago is left alone so an
/// import that has not committed its row yet is never collected.
/// </summary>
[Collection(TestDbFixture.CollectionName)]
public class RawMessageOrphanCollectorTests : IDisposable
{
    private readonly TestDbFixture _fixture;
    private readonly string _basePath;

    public RawMessageOrphanCollectorTests(TestDbFixture fixture)
    {
        _fixture = fixture;
        _basePath = Path.Combine(Path.GetTempPath(), "raworphan-" + Guid.NewGuid().ToString("N")[..8]);
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

    private static byte[] SampleMessage(string marker = "sample")
        => System.Text.Encoding.ASCII.GetBytes(
            "From: a@x.com\r\nTo: b@y.com\r\nSubject: " + marker + "\r\n\r\nBody " + marker + "\r\n");

    private static MailAccount NewAccount() => new()
    {
        Name = "raworphan-" + Guid.NewGuid().ToString("N")[..8],
        EmailAddress = "test@example.com",
        Username = "test@example.com",
        Password = "x",
        Provider = ProviderType.IMPORT,
        IsEnabled = false
    };

    private static ArchivedEmail NewEmail(int accountId) => new()
    {
        MailAccountId = accountId,
        MessageId = "<" + Guid.NewGuid().ToString("N") + "@example.com>",
        Subject = "orphan",
        From = "sender@example.com",
        To = "test@example.com",
        Cc = string.Empty,
        Bcc = string.Empty,
        SentDate = new DateTime(2024, 6, 15, 12, 0, 0),
        ReceivedDate = DateTime.UtcNow,
        IsOutgoing = false,
        HasAttachments = false,
        Body = "body",
        HtmlBody = string.Empty,
        FolderName = "INBOX"
    };

    private RawMessageOrphanCollector Collector(MailArchiverDbContext ctx, RawMessageOptions options)
        => new(ctx,
               new RawMessagePathResolver(options.FileSystem, _basePath),
               Options.Create(options),
               NullLogger<RawMessageOrphanCollector>.Instance);

    [Fact]
    public async Task RemovesAFileNoRowRefersTo()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        var options = BuildOptions();
        var resolver = new RawMessagePathResolver(options.FileSystem, _basePath);

        // A file left behind by a deletion that never removed it.
        var orphan = resolver.GetAbsolutePath(999_001);
        Directory.CreateDirectory(Path.GetDirectoryName(orphan)!);
        await File.WriteAllTextAsync(orphan, "stale");
        File.SetLastWriteTimeUtc(orphan, DateTime.UtcNow.AddDays(-2));

        var result = await Collector(ctx, options).CollectAsync(TimeSpan.FromHours(24), default);

        Assert.Equal(1, result.Removed);
        Assert.False(File.Exists(orphan));

        await scope.RollbackAsync();
    }

    [Fact]
    public async Task KeepsAFileARowStillRefersTo()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        var options = BuildOptions();
        var resolver = new RawMessagePathResolver(options.FileSystem, _basePath);

        var account = NewAccount();
        ctx.MailAccounts.Add(account);
        await ctx.SaveChangesAsync();

        var email = NewEmail(account.Id);
        ctx.ArchivedEmails.Add(email);
        await ctx.SaveChangesAsync();

        var capture = new RawMessageCapture(SampleMessage("keep"), RawMessageSource.Imap);
        var store = new FileSystemRawMessageStore(
            ctx, Options.Create(options), resolver, NullLogger<FileSystemRawMessageStore>.Instance);
        var row = store.CreateRow(email.Id, capture);
        await store.PersistAsync(row!, capture);
        email.RawMessage = row;
        await ctx.SaveChangesAsync();

        var path = resolver.GetAbsolutePath(email.Id);
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-2));

        var result = await Collector(ctx, options).CollectAsync(TimeSpan.FromHours(24), default);

        Assert.Equal(0, result.Removed);
        Assert.True(File.Exists(path));

        await scope.RollbackAsync();
    }

    [Fact]
    public async Task KeepsAFileYoungerThanTheGracePeriod()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        var options = BuildOptions();
        var resolver = new RawMessagePathResolver(options.FileSystem, _basePath);

        // Written a minute ago: its row may not be committed yet.
        var fresh = resolver.GetAbsolutePath(999_002);
        Directory.CreateDirectory(Path.GetDirectoryName(fresh)!);
        await File.WriteAllTextAsync(fresh, "in flight");

        var result = await Collector(ctx, options).CollectAsync(TimeSpan.FromHours(24), default);

        Assert.Equal(0, result.Removed);
        Assert.True(File.Exists(fresh));

        await scope.RollbackAsync();
    }

    [Fact]
    public async Task KeepsAFreshTemporaryFileAndRemovesAnAbandonedOne()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        var options = BuildOptions();
        var resolver = new RawMessagePathResolver(options.FileSystem, _basePath);

        var dir = Path.GetDirectoryName(resolver.GetAbsolutePath(999_003))!;
        Directory.CreateDirectory(dir);

        var fresh = Path.Combine(dir, "999003.eml.gz.tmp");
        await File.WriteAllTextAsync(fresh, "writing");

        var abandoned = Path.Combine(dir, "999004.eml.gz.tmp");
        await File.WriteAllTextAsync(abandoned, "aborted");
        File.SetLastWriteTimeUtc(abandoned, DateTime.UtcNow.AddDays(-3));

        var result = await Collector(ctx, options).CollectAsync(TimeSpan.FromHours(24), default);

        Assert.Equal(1, result.Removed);
        Assert.True(File.Exists(fresh));
        Assert.False(File.Exists(abandoned));

        await scope.RollbackAsync();
    }

    [Fact]
    public async Task MissingBaseDirectoryIsNotAnError()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        var options = BuildOptions();
        Directory.Delete(_basePath, recursive: true);

        var result = await Collector(ctx, options).CollectAsync(TimeSpan.FromHours(24), default);

        Assert.Equal(0, result.Scanned);
        Assert.Equal(0, result.Removed);

        await scope.RollbackAsync();
    }
}

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
/// Tests that deleting an email also removes its stored original. The metadata row goes with
/// the cascade, but a filesystem-backed original is a file outside the database, which no
/// cascade can reach - the whole reason <see cref="IRawMessageStore.DeleteAsync"/> exists.
/// </summary>
[Collection(TestDbFixture.CollectionName)]
public class RawMessageDeletionTests : IDisposable
{
    private readonly TestDbFixture _fixture;
    private readonly string _basePath;

    public RawMessageDeletionTests(TestDbFixture fixture)
    {
        _fixture = fixture;
        _basePath = Path.Combine(Path.GetTempPath(), "rawdel-" + Guid.NewGuid().ToString("N")[..8]);
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

    private RawMessageOptions BuildOptions(string storage) => new()
    {
        Enabled = true,
        Storage = storage,
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
        Name = "rawdel-" + Guid.NewGuid().ToString("N")[..8],
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
        Subject = "raw deletion",
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

    /// <summary>Archives one email with an original in the given backend; returns its id.</summary>
    private async Task<(int EmailId, string Path)> SeedAsync(
        MailArchiverDbContext ctx, RawMessageOptions options, RawMessagePathResolver resolver)
    {
        var account = NewAccount();
        ctx.MailAccounts.Add(account);
        await ctx.SaveChangesAsync();

        var email = NewEmail(account.Id);
        ctx.ArchivedEmails.Add(email);
        await ctx.SaveChangesAsync();

        var capture = new RawMessageCapture(SampleMessage("delete-me"), RawMessageSource.Imap);
        var fs = new FileSystemRawMessageStore(
            ctx, Options.Create(options), resolver, NullLogger<FileSystemRawMessageStore>.Instance);
        var pg = new PostgresRawMessageStore(ctx, Options.Create(options));

        var writer = options.WritesToFileSystem ? (IRawMessageStore)fs : pg;
        var row = writer.CreateRow(email.Id, capture);
        await writer.PersistAsync(row!, capture);

        email.RawMessage = row;
        await ctx.SaveChangesAsync();

        return (email.Id, resolver.GetAbsolutePath(email.Id));
    }

    [Fact]
    public async Task FileSystemBackend_DeleteAsync_RemovesFileAndRow()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        var options = BuildOptions(RawMessageStorage.FileSystem);
        var resolver = new RawMessagePathResolver(options.FileSystem, _basePath);

        var (emailId, path) = await SeedAsync(ctx, options, resolver);
        Assert.True(File.Exists(path));

        var store = new FileSystemRawMessageStore(
            ctx, Options.Create(options), resolver, NullLogger<FileSystemRawMessageStore>.Instance);

        await store.DeleteAsync(emailId);

        Assert.False(File.Exists(path));
        Assert.False(await ctx.RawMessages.AsNoTracking().AnyAsync(r => r.ArchivedEmailId == emailId));

        await scope.RollbackAsync();
    }

    [Fact]
    public async Task FileSystemBackend_DeleteAsync_IsIdempotent()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        var options = BuildOptions(RawMessageStorage.FileSystem);
        var resolver = new RawMessagePathResolver(options.FileSystem, _basePath);

        var (emailId, _) = await SeedAsync(ctx, options, resolver);
        var store = new FileSystemRawMessageStore(
            ctx, Options.Create(options), resolver, NullLogger<FileSystemRawMessageStore>.Instance);

        await store.DeleteAsync(emailId);
        // A second call must not throw; the original is simply gone.
        await store.DeleteAsync(emailId);

        Assert.False(await ctx.RawMessages.AsNoTracking().AnyAsync(r => r.ArchivedEmailId == emailId));

        await scope.RollbackAsync();
    }

    [Fact]
    public async Task FileSystemBackend_DeleteAsync_WithMissingFileStillRemovesRow()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        var options = BuildOptions(RawMessageStorage.FileSystem);
        var resolver = new RawMessagePathResolver(options.FileSystem, _basePath);

        var (emailId, path) = await SeedAsync(ctx, options, resolver);

        // The file vanished (restored backup, manual cleanup); the row must still go.
        File.Delete(path);

        var store = new FileSystemRawMessageStore(
            ctx, Options.Create(options), resolver, NullLogger<FileSystemRawMessageStore>.Instance);

        await store.DeleteAsync(emailId);

        Assert.False(await ctx.RawMessages.AsNoTracking().AnyAsync(r => r.ArchivedEmailId == emailId));

        await scope.RollbackAsync();
    }

    [Fact]
    public async Task PostgresBackend_DeleteAsync_RemovesRow()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        var options = BuildOptions(RawMessageStorage.Postgres);
        var resolver = new RawMessagePathResolver(options.FileSystem, _basePath);

        var (emailId, _) = await SeedAsync(ctx, options, resolver);
        Assert.True(await ctx.RawMessages.AsNoTracking().AnyAsync(r => r.ArchivedEmailId == emailId));

        var store = new PostgresRawMessageStore(ctx, Options.Create(options));
        await store.DeleteAsync(emailId);

        Assert.False(await ctx.RawMessages.AsNoTracking().AnyAsync(r => r.ArchivedEmailId == emailId));

        await scope.RollbackAsync();
    }

    [Fact]
    public async Task Router_DeletesThroughTheBackendTheRowRecords()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;

        // Rows are filesystem-backed while the configured writer is Postgres: the state after
        // a switch that has not been migrated yet. The router must follow the row.
        var options = BuildOptions(RawMessageStorage.Postgres);
        var resolver = new RawMessagePathResolver(options.FileSystem, _basePath);

        var (emailId, path) = await SeedAsync(ctx, options, resolver);

        // Turn the seeded row into a filesystem-backed one, file included.
        var fsOptions = BuildOptions(RawMessageStorage.FileSystem);
        var capture = new RawMessageCapture(SampleMessage("router"), RawMessageSource.Imap);
        var fs = new FileSystemRawMessageStore(
            ctx, Options.Create(fsOptions), resolver, NullLogger<FileSystemRawMessageStore>.Instance);
        var fsRow = fs.CreateRow(emailId, capture);
        await fs.PersistAsync(fsRow!, capture);

        var existing = await ctx.RawMessages.SingleAsync(r => r.ArchivedEmailId == emailId);
        existing.StorageType = RawMessageStorage.FileSystem;
        existing.Content = null;
        existing.StoragePath = fsRow!.StoragePath;
        await ctx.SaveChangesAsync();

        Assert.True(File.Exists(path));

        var router = new RawMessageStoreRouter(
            ctx, Options.Create(options),
            new PostgresRawMessageStore(ctx, Options.Create(options)), fs);

        await router.DeleteAsync(emailId);

        Assert.False(File.Exists(path));
        Assert.False(await ctx.RawMessages.AsNoTracking().AnyAsync(r => r.ArchivedEmailId == emailId));

        await scope.RollbackAsync();
    }
}

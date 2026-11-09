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

    /// <summary>Archives one email with an original; returns the email and its file path.</summary>
    private async Task<(ArchivedEmail Email, string Path)> SeedAsync(
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

        return (email, resolver.GetAbsolutePath(email.Id));
    }

    [Fact]
    public async Task FileSystemBackend_DeletesTheFileAfterTheEmailRowIsGone()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        var options = BuildOptions(RawMessageStorage.FileSystem);
        var resolver = new RawMessagePathResolver(options.FileSystem, _basePath);

        var (email, path) = await SeedAsync(ctx, options, resolver);
        var emailId = email.Id;
        Assert.True(File.Exists(path));

        // The order every delete path uses: row first, then the bytes.
        ctx.ArchivedEmails.Remove(email);
        await ctx.SaveChangesAsync();

        var store = new FileSystemRawMessageStore(
            ctx, Options.Create(options), resolver, NullLogger<FileSystemRawMessageStore>.Instance);
        await store.DeleteAsync(new[] { emailId });

        Assert.False(File.Exists(path));
        Assert.False(await ctx.RawMessages.AsNoTracking().AnyAsync(r => r.ArchivedEmailId == emailId));

        await scope.RollbackAsync();
    }

    [Fact]
    public async Task FileSystemBackend_KeepsTheFileWhileTheEmailRowStillExists()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        var options = BuildOptions(RawMessageStorage.FileSystem);
        var resolver = new RawMessagePathResolver(options.FileSystem, _basePath);

        // Nothing was deleted, so nothing may be removed: a caller must not lose an original
        // because the deletion it attempted failed.
        var (email, path) = await SeedAsync(ctx, options, resolver);

        var store = new FileSystemRawMessageStore(
            ctx, Options.Create(options), resolver, NullLogger<FileSystemRawMessageStore>.Instance);
        await store.DeleteAsync(new[] { email.Id });

        Assert.True(File.Exists(path));
        Assert.True(await store.ExistsAsync(email.Id));

        await scope.RollbackAsync();
    }

    [Fact]
    public async Task FileSystemBackend_IsIdempotentAfterTheRowIsGone()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        var options = BuildOptions(RawMessageStorage.FileSystem);
        var resolver = new RawMessagePathResolver(options.FileSystem, _basePath);

        var (email, path) = await SeedAsync(ctx, options, resolver);
        var emailId = email.Id;

        ctx.ArchivedEmails.Remove(email);
        await ctx.SaveChangesAsync();
        Assert.True(File.Exists(path));

        var store = new FileSystemRawMessageStore(
            ctx, Options.Create(options), resolver, NullLogger<FileSystemRawMessageStore>.Instance);

        // Called twice: a retry after a crash must still find the file by its derived path.
        await store.DeleteAsync(new[] { emailId });
        await store.DeleteAsync(new[] { emailId });

        Assert.False(File.Exists(path));

        await scope.RollbackAsync();
    }

    [Fact]
    public async Task FileSystemBackend_DeletesAWholeBatch()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        var options = BuildOptions(RawMessageStorage.FileSystem);
        var resolver = new RawMessagePathResolver(options.FileSystem, _basePath);

        var (first, firstPath) = await SeedAsync(ctx, options, resolver);
        var (second, secondPath) = await SeedAsync(ctx, options, resolver);
        var ids = new[] { first.Id, second.Id };

        ctx.ArchivedEmails.RemoveRange(first, second);
        await ctx.SaveChangesAsync();

        var store = new FileSystemRawMessageStore(
            ctx, Options.Create(options), resolver, NullLogger<FileSystemRawMessageStore>.Instance);
        await store.DeleteAsync(ids);

        Assert.False(File.Exists(firstPath));
        Assert.False(File.Exists(secondPath));

        await scope.RollbackAsync();
    }

    [Fact]
    public async Task FileSystemBackend_MissingFileIsNotAnError()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        var options = BuildOptions(RawMessageStorage.FileSystem);
        var resolver = new RawMessagePathResolver(options.FileSystem, _basePath);

        var (email, path) = await SeedAsync(ctx, options, resolver);
        var emailId = email.Id;

        // The file vanished (restored backup, manual cleanup); deleting must still succeed.
        File.Delete(path);

        ctx.ArchivedEmails.Remove(email);
        await ctx.SaveChangesAsync();

        var store = new FileSystemRawMessageStore(
            ctx, Options.Create(options), resolver, NullLogger<FileSystemRawMessageStore>.Instance);
        await store.DeleteAsync(new[] { emailId });

        Assert.False(await ctx.RawMessages.AsNoTracking().AnyAsync(r => r.ArchivedEmailId == emailId));

        await scope.RollbackAsync();
    }

    [Fact]
    public async Task PostgresBackend_DeleteAsyncDoesNothing()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        var options = BuildOptions(RawMessageStorage.Postgres);
        var resolver = new RawMessagePathResolver(options.FileSystem, _basePath);

        var (email, _) = await SeedAsync(ctx, options, resolver);
        var emailId = email.Id;

        ctx.ArchivedEmails.Remove(email);
        await ctx.SaveChangesAsync();

        var store = new PostgresRawMessageStore(ctx, Options.Create(options));
        await store.DeleteAsync(new[] { emailId });

        // The row went with the cascade; the store has nothing left to do.
        Assert.False(await ctx.RawMessages.AsNoTracking().AnyAsync(r => r.ArchivedEmailId == emailId));

        await scope.RollbackAsync();
    }

    [Fact]
    public async Task Router_DeletesFilesEvenWhenTheWriterIsPostgres()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;

        // Rows written while the filesystem backend was configured, now the writer is Postgres.
        // After the email is gone there is no storage type left to read, so the router must
        // still remove the file.
        var fsOptions = BuildOptions(RawMessageStorage.FileSystem);
        var resolver = new RawMessagePathResolver(fsOptions.FileSystem, _basePath);
        var (email, path) = await SeedAsync(ctx, fsOptions, resolver);
        var emailId = email.Id;

        ctx.ArchivedEmails.Remove(email);
        await ctx.SaveChangesAsync();
        Assert.True(File.Exists(path));

        var pgOptions = BuildOptions(RawMessageStorage.Postgres);
        var router = new RawMessageStoreRouter(
            ctx, Options.Create(pgOptions),
            new PostgresRawMessageStore(ctx, Options.Create(pgOptions)),
            new FileSystemRawMessageStore(
                ctx, Options.Create(fsOptions), resolver, NullLogger<FileSystemRawMessageStore>.Instance));

        await router.DeleteAsync(new[] { emailId });

        Assert.False(File.Exists(path));

        await scope.RollbackAsync();
    }
}

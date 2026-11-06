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
/// Tests for the pluggable original-message storage: the filesystem backend, the router that
/// follows each row's storage type, and the migration between the two backends.
/// </summary>
[Collection(TestDbFixture.CollectionName)]
public class RawMessageStorageBackendTests : IDisposable
{
    private readonly TestDbFixture _fixture;
    private readonly string _basePath;

    public RawMessageStorageBackendTests(TestDbFixture fixture)
    {
        _fixture = fixture;
        _basePath = Path.Combine(Path.GetTempPath(), "rawstore-" + Guid.NewGuid().ToString("N")[..8]);
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

    private RawMessagePathResolver Resolver(RawMessageOptions options)
        => new(options.FileSystem, _basePath);

    private static byte[] SampleMessage(string marker = "sample")
        => System.Text.Encoding.ASCII.GetBytes(
            "From: a@x.com\r\nTo: b@y.com\r\nSubject: " + marker + "\r\n\r\nBody " + marker + "\r\n");

    private static MailAccount NewAccount() => new()
    {
        Name = "rawstore-" + Guid.NewGuid().ToString("N")[..8],
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
        Subject = "raw storage",
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

    [Fact]
    public void PathResolver_IsDeterministicAndSharded()
    {
        var resolver = Resolver(BuildOptions(RawMessageStorage.FileSystem));

        // id 1234 -> least significant pair first, then the file name.
        Assert.Equal("34/12/1234.eml.gz", resolver.GetRelativePath(1234));
        Assert.Equal("34/12/1234.eml.gz", resolver.GetRelativePath(1234));
    }

    [Fact]
    public void PathResolver_ShardDepthZero_IsFlat()
    {
        var options = BuildOptions(RawMessageStorage.FileSystem);
        options.FileSystem.ShardDepth = 0;

        Assert.Equal("7.eml.gz", Resolver(options).GetRelativePath(7));
    }

    [Fact]
    public void PathResolver_RefusesPathsOutsideTheBaseDirectory()
    {
        var resolver = Resolver(BuildOptions(RawMessageStorage.FileSystem));

        Assert.Throws<InvalidOperationException>(() => resolver.GetAbsolutePath("../../etc/passwd"));
    }

    [Fact]
    public async Task FileSystemStore_WritesReadsAndVerifies()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;

        var account = NewAccount();
        ctx.MailAccounts.Add(account);
        await ctx.SaveChangesAsync();

        var email = NewEmail(account.Id);
        ctx.ArchivedEmails.Add(email);
        await ctx.SaveChangesAsync();

        var options = BuildOptions(RawMessageStorage.FileSystem);
        var store = new FileSystemRawMessageStore(
            ctx, Options.Create(options), Resolver(options), NullLogger<FileSystemRawMessageStore>.Instance);

        var capture = new RawMessageCapture(SampleMessage(), RawMessageSource.Imap);
        var row = store.CreateRow(email.Id, capture);

        Assert.NotNull(row);
        Assert.Null(row!.Content);
        Assert.Equal(RawMessageStorage.FileSystem, row.StorageType);

        await store.PersistAsync(row, capture);
        email.RawMessage = row;
        await ctx.SaveChangesAsync();

        Assert.True(await store.ExistsAsync(email.Id));

        var content = await store.ReadAsync(email.Id);
        Assert.NotNull(content);
        Assert.Equal(SampleMessage(), content!.Bytes);
        Assert.Equal(RawMessageStorage.FileSystem, content.StorageType);

        await scope.RollbackAsync();
    }

    [Fact]
    public async Task FileSystemStore_DetectsATamperedFile()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;

        var account = NewAccount();
        ctx.MailAccounts.Add(account);
        await ctx.SaveChangesAsync();

        var email = NewEmail(account.Id);
        ctx.ArchivedEmails.Add(email);
        await ctx.SaveChangesAsync();

        var options = BuildOptions(RawMessageStorage.FileSystem);
        var resolver = Resolver(options);
        var store = new FileSystemRawMessageStore(
            ctx, Options.Create(options), resolver, NullLogger<FileSystemRawMessageStore>.Instance);

        var capture = new RawMessageCapture(SampleMessage("original"), RawMessageSource.Imap);
        var row = store.CreateRow(email.Id, capture)!;
        await store.PersistAsync(row, capture);
        email.RawMessage = row;
        await ctx.SaveChangesAsync();

        // Replace the file with different, valid gzip content - what someone with filesystem
        // access would do. The row still records the original checksum.
        await File.WriteAllBytesAsync(
            resolver.GetAbsolutePath(email.Id), RawMessageCodec.Compress(SampleMessage("tampered")));

        var ex = await Assert.ThrowsAsync<RawMessageIntegrityException>(() => store.ReadAsync(email.Id));
        Assert.Equal(email.Id, ex.EmailId);

        await scope.RollbackAsync();
    }

    [Fact]
    public async Task FileSystemStore_MissingFile_ReadsAsAbsent()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;

        var account = NewAccount();
        ctx.MailAccounts.Add(account);
        await ctx.SaveChangesAsync();

        var email = NewEmail(account.Id);
        ctx.ArchivedEmails.Add(email);
        await ctx.SaveChangesAsync();

        var options = BuildOptions(RawMessageStorage.FileSystem);
        var resolver = Resolver(options);
        var store = new FileSystemRawMessageStore(
            ctx, Options.Create(options), resolver, NullLogger<FileSystemRawMessageStore>.Instance);

        var capture = new RawMessageCapture(SampleMessage(), RawMessageSource.Imap);
        var row = store.CreateRow(email.Id, capture)!;
        await store.PersistAsync(row, capture);
        email.RawMessage = row;
        await ctx.SaveChangesAsync();

        File.Delete(resolver.GetAbsolutePath(email.Id));

        Assert.False(await store.ExistsAsync(email.Id));
        Assert.Null(await store.ReadAsync(email.Id));

        await scope.RollbackAsync();
    }

    [Fact]
    public async Task Router_ReadsRowsWrittenByEitherBackend()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;

        var account = NewAccount();
        ctx.MailAccounts.Add(account);
        await ctx.SaveChangesAsync();

        var options = BuildOptions(RawMessageStorage.FileSystem);
        var resolver = Resolver(options);
        var postgres = new PostgresRawMessageStore(ctx, Options.Create(options));
        var fileSystem = new FileSystemRawMessageStore(
            ctx, Options.Create(options), resolver, NullLogger<FileSystemRawMessageStore>.Instance);
        var router = new RawMessageStoreRouter(ctx, Options.Create(options), postgres, fileSystem);

        // A Postgres row written before the switch.
        var legacyEmail = NewEmail(account.Id);
        ctx.ArchivedEmails.Add(legacyEmail);
        await ctx.SaveChangesAsync();

        var legacyCapture = new RawMessageCapture(SampleMessage("legacy"), RawMessageSource.EmlFile);
        legacyEmail.RawMessage = postgres.CreateRow(legacyEmail.Id, legacyCapture);
        await ctx.SaveChangesAsync();

        // A filesystem row written after switching the backend.
        var fsEmail = NewEmail(account.Id);
        ctx.ArchivedEmails.Add(fsEmail);
        await ctx.SaveChangesAsync();

        var fsCapture = new RawMessageCapture(SampleMessage("fs"), RawMessageSource.Imap);
        var fsRow = fileSystem.CreateRow(fsEmail.Id, fsCapture)!;
        await fileSystem.PersistAsync(fsRow, fsCapture);
        fsEmail.RawMessage = fsRow;
        await ctx.SaveChangesAsync();

        // The router serves both, each from its own backend.
        var legacyContent = await router.ReadAsync(legacyEmail.Id);
        Assert.Equal(SampleMessage("legacy"), legacyContent!.Bytes);
        Assert.Equal(RawMessageStorage.Postgres, legacyContent.StorageType);

        var fsContent = await router.ReadAsync(fsEmail.Id);
        Assert.Equal(SampleMessage("fs"), fsContent!.Bytes);
        Assert.Equal(RawMessageStorage.FileSystem, fsContent.StorageType);

        await scope.RollbackAsync();
    }

    [Fact]
    public async Task Migrator_MovesBothWaysAndKeepsTheChecksum()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;

        var account = NewAccount();
        ctx.MailAccounts.Add(account);
        await ctx.SaveChangesAsync();

        var options = BuildOptions(RawMessageStorage.Postgres);
        var resolver = Resolver(options);
        var migrator = new RawMessageStorageMigrator(
            ctx, Options.Create(options), resolver, NullLogger<RawMessageStorageMigrator>.Instance);

        var capture = new RawMessageCapture(SampleMessage("migrate"), RawMessageSource.Imap);
        var email = NewEmail(account.Id);
        ctx.ArchivedEmails.Add(email);
        await ctx.SaveChangesAsync();

        email.RawMessage = RawMessageCodec.Create(capture, options);
        await ctx.SaveChangesAsync();

        var originalHash = email.RawMessage!.Sha256;

        // Postgres -> filesystem
        var toFs = await migrator.MigrateAsync(RawMessageStorage.Postgres, RawMessageStorage.FileSystem);
        Assert.Equal(1, toFs.Moved);
        Assert.Equal(0, toFs.Failed);

        var row = await ctx.RawMessages.AsNoTracking().SingleAsync(r => r.ArchivedEmailId == email.Id);
        Assert.Equal(RawMessageStorage.FileSystem, row.StorageType);
        Assert.Null(row.Content);
        Assert.True(File.Exists(resolver.GetAbsolutePath(email.Id)));
        Assert.Equal(originalHash, row.Sha256);

        // filesystem -> Postgres
        var toPg = await migrator.MigrateAsync(RawMessageStorage.FileSystem, RawMessageStorage.Postgres);
        Assert.Equal(1, toPg.Moved);

        var back = await ctx.RawMessages.AsNoTracking().SingleAsync(r => r.ArchivedEmailId == email.Id);
        Assert.Equal(RawMessageStorage.Postgres, back.StorageType);
        Assert.NotNull(back.Content);
        Assert.False(File.Exists(resolver.GetAbsolutePath(email.Id)));

        var bytes = RawMessageCodec.Decompress(back);
        Assert.Equal(SampleMessage("migrate"), bytes);
        Assert.Equal(originalHash, RawMessageCodec.ComputeSha256(bytes));

        await scope.RollbackAsync();
    }

    [Fact]
    public async Task Migrator_LeavesACorruptFileInPlace()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;

        var account = NewAccount();
        ctx.MailAccounts.Add(account);
        await ctx.SaveChangesAsync();

        var options = BuildOptions(RawMessageStorage.FileSystem);
        var resolver = Resolver(options);
        var fileSystem = new FileSystemRawMessageStore(
            ctx, Options.Create(options), resolver, NullLogger<FileSystemRawMessageStore>.Instance);
        var migrator = new RawMessageStorageMigrator(
            ctx, Options.Create(options), resolver, NullLogger<RawMessageStorageMigrator>.Instance);

        var email = NewEmail(account.Id);
        ctx.ArchivedEmails.Add(email);
        await ctx.SaveChangesAsync();

        var capture = new RawMessageCapture(SampleMessage("good"), RawMessageSource.Imap);
        var row = fileSystem.CreateRow(email.Id, capture)!;
        await fileSystem.PersistAsync(row, capture);
        email.RawMessage = row;
        await ctx.SaveChangesAsync();

        // Corrupt the file behind the archive's back.
        await File.WriteAllBytesAsync(
            resolver.GetAbsolutePath(email.Id), RawMessageCodec.Compress(SampleMessage("bad")));

        var result = await migrator.MigrateAsync(RawMessageStorage.FileSystem, RawMessageStorage.Postgres);

        Assert.Equal(0, result.Moved);
        Assert.Equal(1, result.Failed);
        Assert.NotEmpty(result.Errors);

        // Neither the row nor the file may be touched: the source is the only copy left.
        var unchanged = await ctx.RawMessages.AsNoTracking().SingleAsync(r => r.ArchivedEmailId == email.Id);
        Assert.Equal(RawMessageStorage.FileSystem, unchanged.StorageType);
        Assert.True(File.Exists(resolver.GetAbsolutePath(email.Id)));

        await scope.RollbackAsync();
    }
}

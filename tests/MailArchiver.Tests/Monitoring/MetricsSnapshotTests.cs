using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services.Monitoring;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MailArchiver.Tests.Monitoring;

public class MetricsSnapshotTests
{
    private sealed class SqliteDb : IDisposable
    {
        private readonly SqliteConnection _connection;
        public MailArchiverDbContext Context { get; }

        public SqliteDb()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<MailArchiverDbContext>()
                .UseSqlite(_connection)
                .ConfigureWarnings(w => w.Ignore(
                    Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
                .Options;

            Context = new MailArchiverDbContext(options);
            Context.Database.EnsureCreated();
        }

        public void Dispose()
        {
            Context.Dispose();
            _connection.Dispose();
        }
    }

    private static MailAccount Account(string name, bool enabled, DateTime lastSync) => new()
    {
        Name = name,
        EmailAddress = $"{name.ToLowerInvariant()}@example.com",
        IsEnabled = enabled,
        LastSync = lastSync
    };

    private static ArchivedEmail Email(int accountId, string subject) => new()
    {
        MailAccountId = accountId,
        MessageId = $"<{Guid.NewGuid():N}@example.com>",
        Subject = subject,
        Body = string.Empty,
        HtmlBody = string.Empty,
        From = "sender@example.com",
        To = "recipient@example.com",
        Cc = string.Empty,
        Bcc = string.Empty,
        FolderName = "INBOX"
    };

    [Fact]
    public async Task BuildSnapshot_CountsEmailsAccountsAndMissingOriginals()
    {
        using var db = new SqliteDb();
        var synced = Account("Archive", true, DateTime.UtcNow.AddMinutes(-5));
        var disabled = Account("Old", false, default);
        db.Context.MailAccounts.AddRange(synced, disabled);
        db.Context.SaveChanges();

        var first = Email(synced.Id, "one");
        var second = Email(synced.Id, "two");
        var third = Email(synced.Id, "three");
        db.Context.ArchivedEmails.AddRange(first, second, third);
        db.Context.SaveChanges();

        db.Context.RawMessages.AddRange(
            new RawMessage
            {
                ArchivedEmailId = first.Id,
                Sha256 = new string('a', 64),
                Size = 1,
                Source = RawMessageSource.Imap,
                StorageType = RawMessageStorage.Postgres,
                Content = new byte[] { 1 }
            },
            new RawMessage
            {
                ArchivedEmailId = second.Id,
                Sha256 = new string('b', 64),
                Size = 1,
                Source = RawMessageSource.Imap,
                StorageType = RawMessageStorage.Postgres,
                Content = new byte[] { 2 }
            });
        db.Context.SaveChanges();

        var snapshot = await MetricsRefreshService.BuildSnapshotAsync(
            db.Context, rawMessagesEnabled: true, CancellationToken.None);

        Assert.Equal(3, snapshot.Emails);
        Assert.Equal(1, snapshot.EnabledAccounts);
        Assert.Equal(1, snapshot.DisabledAccounts);
        Assert.Equal(2, snapshot.RawMessages);
        Assert.Equal(1, snapshot.MissingOriginals);
        Assert.True(snapshot.RawMessagesEnabled);
        Assert.Equal(2, snapshot.Accounts.Count);
        Assert.Equal("Archive", snapshot.Accounts[0].Name);
        Assert.Equal(default(DateTime), snapshot.Accounts[1].LastSync);
    }

    [Fact]
    public async Task BuildSnapshot_WithRawStorageDisabled_StillCountsExistingRows()
    {
        using var db = new SqliteDb();
        var account = Account("Archive", true, DateTime.UtcNow);
        db.Context.MailAccounts.Add(account);
        db.Context.SaveChanges();

        db.Context.ArchivedEmails.Add(Email(account.Id, "one"));
        db.Context.SaveChanges();

        var snapshot = await MetricsRefreshService.BuildSnapshotAsync(
            db.Context, rawMessagesEnabled: false, CancellationToken.None);

        Assert.Equal(1, snapshot.Emails);
        Assert.Equal(0, snapshot.RawMessages);
        Assert.Equal(1, snapshot.MissingOriginals);
        Assert.False(snapshot.RawMessagesEnabled);
    }
}

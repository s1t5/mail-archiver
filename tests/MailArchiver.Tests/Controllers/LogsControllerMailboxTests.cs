using MailArchiver.Controllers;
using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace MailArchiver.Tests.Controllers;

/// <summary>
/// Regression tests for issue #605: a deletion or retention entry in the access log names
/// the mailbox it refers to. The log row stores only <see cref="AccessLog.MailAccountId"/>,
/// so the controller has to resolve the name for the view.
/// </summary>
public class LogsControllerMailboxTests
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

    private sealed class PassThroughLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();
    }

    private static LogsController CreateController(MailArchiverDbContext context, string username, bool isAdmin)
    {
        var auth = new Mock<IAuthenticationService>();
        auth.Setup(a => a.GetCurrentUserDisplayName(It.IsAny<HttpContext>())).Returns(username);
        auth.Setup(a => a.IsCurrentUserAdmin(It.IsAny<HttpContext>())).Returns(isAdmin);

        return new LogsController(
            new AccessLogService(context),
            auth.Object,
            context,
            new Mock<IAuditExportService>().Object,
            Options.Create(new AuditExportOptions()),
            new PassThroughLocalizer())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    private static MailAccount AddAccount(MailArchiverDbContext context, string name)
    {
        var account = new MailAccount
        {
            Name = name,
            EmailAddress = $"{name.ToLowerInvariant()}@example.com"
        };
        context.MailAccounts.Add(account);
        context.SaveChanges();
        return account;
    }

    [Fact]
    public async Task Index_ResolvesMailboxNameForRetentionEntry()
    {
        using var db = new SqliteDb();
        var account = AddAccount(db.Context, "Archive");
        db.Context.AccessLogs.Add(new AccessLog
        {
            Username = "System",
            Type = AccessLogType.Retention,
            Timestamp = DateTime.UtcNow,
            SearchParameters = "Retention (IMAP): Deleted 3 emails older than 180 days from mailbox",
            MailAccountId = account.Id
        });
        db.Context.SaveChanges();

        var controller = CreateController(db.Context, "admin", isAdmin: true);

        var result = await controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        var logs = Assert.IsAssignableFrom<List<AccessLog>>(view.Model);
        Assert.Single(logs);

        var names = Assert.IsType<Dictionary<int, string>>(controller.ViewBag.MailAccountNames);
        Assert.Equal("Archive", names[account.Id]);
    }

    [Fact]
    public async Task Index_OmitsEntriesWithoutMailboxAndKeepsDeletedAccountOutOfTheMap()
    {
        using var db = new SqliteDb();
        var account = AddAccount(db.Context, "Archive");
        db.Context.AccessLogs.AddRange(
            new AccessLog
            {
                Username = "admin",
                Type = AccessLogType.Login,
                Timestamp = DateTime.UtcNow
            },
            new AccessLog
            {
                Username = "System",
                Type = AccessLogType.Retention,
                Timestamp = DateTime.UtcNow,
                SearchParameters = "Retention (IMAP): Deleted 1 emails older than 365 days from mailbox",
                // The account this entry pointed at was deleted; the id remains in the log.
                MailAccountId = 999
            });
        db.Context.SaveChanges();

        var controller = CreateController(db.Context, "admin", isAdmin: true);

        await controller.Index();

        var names = Assert.IsType<Dictionary<int, string>>(controller.ViewBag.MailAccountNames);
        Assert.False(names.ContainsKey(999));
        Assert.Empty(names); // the seeded account is referenced by no row, so nothing is resolved
    }

    [Fact]
    public async Task Index_NonAdminSeesOnlyOwnLogsButStillGetsMailboxName()
    {
        using var db = new SqliteDb();
        var account = AddAccount(db.Context, "Archive");
        db.Context.AccessLogs.AddRange(
            new AccessLog
            {
                Username = "alice",
                Type = AccessLogType.Retention,
                Timestamp = DateTime.UtcNow,
                MailAccountId = account.Id
            },
            new AccessLog
            {
                Username = "bob",
                Type = AccessLogType.Retention,
                Timestamp = DateTime.UtcNow,
                MailAccountId = account.Id
            });
        db.Context.SaveChanges();

        var controller = CreateController(db.Context, "alice", isAdmin: false);

        var result = await controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        var logs = Assert.IsAssignableFrom<List<AccessLog>>(view.Model);
        Assert.Single(logs);
        Assert.Equal("alice", logs[0].Username);

        var names = Assert.IsType<Dictionary<int, string>>(controller.ViewBag.MailAccountNames);
        Assert.Equal("Archive", names[account.Id]);
    }
}

using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services;
using MailArchiver.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace MailArchiver.Tests.Services;

/// <summary>
/// Runs <see cref="EmailDeletionService"/> against the PostgreSQL test database and checks
/// that every deleted email leaves exactly one Deletion entry in the access log, and that a
/// failing delete never leaves access log rows behind.
/// Every test runs inside a rolled-back transaction: the service resolves the test's own
/// context, so its writes stay inside that transaction and nothing has to be cleaned up
/// afterwards. That also covers the locked-email case, whose rows no cleanup could delete
/// because the compliance trigger refuses it.
/// </summary>
[Collection(TestDbFixture.CollectionName)]
public class EmailDeletionServiceTests
{
    private readonly TestDbFixture _fixture;
    public EmailDeletionServiceTests(TestDbFixture fixture) => _fixture = fixture;

    /// <summary>
    /// Builds a service whose scopes resolve the test's context and access log service, so
    /// every write the background job makes lands in the caller's transaction.
    /// </summary>
    private static EmailDeletionService CreateService(MailArchiverDbContext sharedContext)
    {
        var services = new ServiceCollection();
        services.AddSingleton(sharedContext);
        services.AddScoped<IAccessLogService, AccessLogService>();
        services.AddSingleton<IOptions<DeletionPolicyOptions>>(_ =>
            Options.Create(new DeletionPolicyOptions { DeletionAllowed = true }));
        var provider = services.BuildServiceProvider();

        return new EmailDeletionService(
            NullLogger<EmailDeletionService>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());
    }

    private static async Task<MailAccount> SeedAccountAsync(MailArchiverDbContext ctx)
    {
        var account = new MailAccount
        {
            Name = $"deltest-{Guid.NewGuid():N}".Substring(0, 25),
            EmailAddress = $"{Guid.NewGuid():N}@deltest.local",
            Provider = ProviderType.IMPORT,
            IsEnabled = false,
            LastSync = DateTime.UtcNow
        };
        ctx.MailAccounts.Add(account);
        await ctx.SaveChangesAsync();
        return account;
    }

    private static ArchivedEmail NewEmail(int accountId, string subject, bool isLocked = false) => new()
    {
        MailAccountId = accountId,
        MessageId = $"<{Guid.NewGuid():N}@deltest.local>",
        Subject = subject,
        Body = "body",
        HtmlBody = string.Empty,
        From = "sender@deltest.local",
        To = "b@deltest.local",
        Cc = string.Empty,
        Bcc = string.Empty,
        FolderName = "INBOX",
        SentDate = DateTime.UtcNow.AddDays(-1),
        ReceivedDate = DateTime.UtcNow,
        IsLocked = isLocked
    };

    /// <summary>
    /// Runs the background loop until the queued job reaches a terminal state. Only the
    /// in-memory job object is polled: the service shares the test connection, which allows
    /// one command at a time.
    /// </summary>
    private static async Task<EmailDeletionJob> RunJobAsync(EmailDeletionService service, EmailDeletionJob job)
    {
        await service.StartAsync(CancellationToken.None);
        try
        {
            service.QueueJob(job);
            var deadline = DateTime.UtcNow.AddSeconds(60);
            while (!job.IsCompleted && DateTime.UtcNow < deadline)
            {
                await Task.Delay(250);
            }
            return job;
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task DeletionJob_RemovesEmails_AndWritesOneAccessLogPerEmail()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var username = $"deltest-{Guid.NewGuid():N}";
            var account = await SeedAccountAsync(ctx);
            var longSubject = new string('s', 300);
            var toDelete = new[]
            {
                NewEmail(account.Id, "first"),
                NewEmail(account.Id, "second"),
                NewEmail(account.Id, longSubject)
            };
            var keep = NewEmail(account.Id, "keep");
            ctx.ArchivedEmails.AddRange(toDelete);
            ctx.ArchivedEmails.Add(keep);
            await ctx.SaveChangesAsync();
            var emailIds = toDelete.Select(e => e.Id).ToList();

            var job = await RunJobAsync(CreateService(ctx),
                new EmailDeletionJob { UserId = username, EmailIds = emailIds });

            Assert.Equal(EmailDeletionJobStatus.Completed, job.Status);
            Assert.Equal(3, job.DeletedEmails);

            ctx.ChangeTracker.Clear();
            Assert.False(await ctx.ArchivedEmails.AnyAsync(e => emailIds.Contains(e.Id)));
            Assert.True(await ctx.ArchivedEmails.AnyAsync(e => e.Id == keep.Id));

            var logs = await ctx.AccessLogs.AsNoTracking()
                .Where(l => l.Username == username)
                .ToListAsync();
            Assert.Equal(3, logs.Count);
            Assert.All(logs, l => Assert.Equal(AccessLogType.Deletion, l.Type));
            Assert.Equal(emailIds.OrderBy(id => id), logs.Select(l => l.EmailId!.Value).OrderBy(id => id));
            Assert.Equal(255, logs.Single(l => l.EmailId == toDelete[2].Id).EmailSubject!.Length);
            Assert.All(logs, l => Assert.Equal(account.Id, l.MailAccountId));
            Assert.All(logs, l => Assert.Equal("sender@deltest.local", l.EmailFrom));
        }
        finally
        {
            await scope.RollbackAsync();
        }
    }

    /// <summary>
    /// A locked email cannot be deleted: the compliance trigger raises. The batch must fail as
    /// a whole, and because the log rows share the delete's SaveChangesAsync none of them may
    /// be committed - otherwise the access log would list emails that are still archived.
    /// </summary>
    [Fact]
    public async Task DeletionJob_WhenDeleteFails_WritesNoAccessLog()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var username = $"deltest-{Guid.NewGuid():N}";
            var account = await SeedAccountAsync(ctx);
            var unlocked = NewEmail(account.Id, "unlocked");
            var locked = NewEmail(account.Id, "locked", isLocked: true);
            ctx.ArchivedEmails.AddRange(unlocked, locked);
            await ctx.SaveChangesAsync();
            var emailIds = new List<int> { unlocked.Id, locked.Id };

            var job = await RunJobAsync(CreateService(ctx),
                new EmailDeletionJob { UserId = username, EmailIds = emailIds });

            Assert.Equal(EmailDeletionJobStatus.Failed, job.Status);

            ctx.ChangeTracker.Clear();
            Assert.Equal(2, await ctx.ArchivedEmails.CountAsync(e => emailIds.Contains(e.Id)));
            Assert.False(await ctx.AccessLogs.AnyAsync(l => l.Username == username));
        }
        finally
        {
            await scope.RollbackAsync();
        }
    }

    /// <summary>
    /// An access log insert that the database rejects must not leave a partial batch behind:
    /// no email may be deleted without its log entry, so the whole batch fails and nothing
    /// is removed.
    /// </summary>
    [Fact]
    public async Task DeletionJob_WhenAccessLogWriteFails_DeletesNothing()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            // Make the access log insert fail for one subject. DDL is transactional in
            // PostgreSQL, so the trigger disappears again with the rollback.
            await ctx.Database.ExecuteSqlRawAsync(@"
                CREATE FUNCTION mail_archiver.test_reject_access_log_row() RETURNS trigger AS $$
                BEGIN
                    IF NEW.""EmailSubject"" = 'audit-fails' THEN
                        RAISE EXCEPTION 'access log write rejected by test';
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
                CREATE TRIGGER test_reject_access_log_row BEFORE INSERT ON mail_archiver.""AccessLogs""
                    FOR EACH ROW EXECUTE FUNCTION mail_archiver.test_reject_access_log_row();");

            var username = $"deltest-{Guid.NewGuid():N}";
            var account = await SeedAccountAsync(ctx);
            var emails = new[]
            {
                NewEmail(account.Id, "first"),
                NewEmail(account.Id, "audit-fails"),
                NewEmail(account.Id, "third")
            };
            ctx.ArchivedEmails.AddRange(emails);
            await ctx.SaveChangesAsync();
            var emailIds = emails.Select(e => e.Id).ToList();

            var job = await RunJobAsync(CreateService(ctx),
                new EmailDeletionJob { UserId = username, EmailIds = emailIds });

            Assert.Equal(EmailDeletionJobStatus.Failed, job.Status);
            Assert.Equal(0, job.DeletedEmails);

            ctx.ChangeTracker.Clear();
            Assert.Equal(3, await ctx.ArchivedEmails.CountAsync(e => emailIds.Contains(e.Id)));
            Assert.False(await ctx.AccessLogs.AnyAsync(l => l.Username == username));
        }
        finally
        {
            await scope.RollbackAsync();
        }
    }
}

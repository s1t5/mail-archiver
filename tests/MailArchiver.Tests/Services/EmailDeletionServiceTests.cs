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
/// that every deleted email leaves exactly one Deletion entry in the access log.
/// </summary>
[Collection(TestDbFixture.CollectionName)]
public class EmailDeletionServiceTests
{
    private readonly TestDbFixture _fixture;
    public EmailDeletionServiceTests(TestDbFixture fixture) => _fixture = fixture;

    private EmailDeletionService CreateService()
    {
        var services = new ServiceCollection();
        services.AddDbContext<MailArchiverDbContext>(options => options
            .UseNpgsql(_fixture.ConnectionString)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));
        services.AddScoped<IAccessLogService, AccessLogService>();
        services.AddSingleton<IOptions<DeletionPolicyOptions>>(_ =>
            Options.Create(new DeletionPolicyOptions { DeletionAllowed = true }));
        var provider = services.BuildServiceProvider();

        return new EmailDeletionService(
            NullLogger<EmailDeletionService>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());
    }

    [Fact]
    public async Task DeletionJob_RemovesEmails_AndWritesOneAccessLogPerEmail()
    {
        var ctx = _fixture.CreateContext();
        var username = $"deltest-{Guid.NewGuid():N}";
        var service = CreateService();
        try
        {
            var account = new MailAccount
            {
                Name = $"acct-{Guid.NewGuid():N}".Substring(0, 25),
                EmailAddress = $"{Guid.NewGuid():N}@test.local",
                Provider = ProviderType.IMAP,
                IsEnabled = true,
                LastSync = DateTime.UtcNow
            };
            ctx.MailAccounts.Add(account);
            await ctx.SaveChangesAsync();

            var emails = Enumerable.Range(0, 3).Select(i => new ArchivedEmail
            {
                MailAccountId = account.Id,
                MessageId = Guid.NewGuid().ToString(),
                Subject = i == 0 ? new string('s', 300) : $"subject {i}",
                From = $"sender{i}@x.com",
                To = "b@x.com",
                Cc = string.Empty,
                Bcc = string.Empty,
                Body = "body",
                HtmlBody = string.Empty,
                SentDate = DateTime.UtcNow.AddDays(-1),
                ReceivedDate = DateTime.UtcNow,
                FolderName = "INBOX"
            }).ToList();
            ctx.ArchivedEmails.AddRange(emails);
            await ctx.SaveChangesAsync();
            var emailIds = emails.Select(e => e.Id).ToList();

            // The DB locks new rows by default; unlocking is explicitly allowed by the trigger.
            await ctx.ArchivedEmails
                .Where(e => emailIds.Contains(e.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.IsLocked, false));

            await service.StartAsync(CancellationToken.None);
            var jobId = service.QueueJob(new EmailDeletionJob { UserId = username, EmailIds = emailIds });

            var deadline = DateTime.UtcNow.AddSeconds(60);
            EmailDeletionJob? job;
            do
            {
                await Task.Delay(250);
                job = service.GetJob(jobId);
            } while (job != null
                     && job.Status is EmailDeletionJobStatus.Queued or EmailDeletionJobStatus.Running
                     && DateTime.UtcNow < deadline);

            Assert.NotNull(job);
            Assert.Equal(EmailDeletionJobStatus.Completed, job!.Status);
            Assert.Equal(3, job.DeletedEmails);

            await using var verifyCtx = _fixture.CreateContext();
            Assert.False(await verifyCtx.ArchivedEmails.AnyAsync(e => emailIds.Contains(e.Id)));

            var logs = await verifyCtx.AccessLogs.AsNoTracking()
                .Where(l => l.Username == username)
                .ToListAsync();
            Assert.Equal(3, logs.Count);
            Assert.All(logs, l => Assert.Equal(AccessLogType.Deletion, l.Type));
            Assert.Equal(emailIds.OrderBy(id => id), logs.Select(l => l.EmailId!.Value).OrderBy(id => id));
            Assert.Equal(255, logs.Single(l => l.EmailId == emailIds[0]).EmailSubject!.Length);
            Assert.All(logs, l => Assert.Equal(account.Id, l.MailAccountId));
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);

            // The seeded emails are still tracked although the service deleted them.
            ctx.ChangeTracker.Clear();

            var logs = await ctx.AccessLogs.Where(l => l.Username == username).ToListAsync();
            ctx.AccessLogs.RemoveRange(logs);
            var accounts = await ctx.MailAccounts.Where(a => a.EmailAddress.EndsWith("@test.local")).ToListAsync();
            var accountIds = accounts.Select(a => a.Id).ToList();
            await ctx.ArchivedEmails
                .Where(e => accountIds.Contains(e.MailAccountId) && e.IsLocked)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.IsLocked, false));
            ctx.ArchivedEmails.RemoveRange(await ctx.ArchivedEmails.Where(e => accountIds.Contains(e.MailAccountId)).ToListAsync());
            ctx.MailAccounts.RemoveRange(accounts);
            await ctx.SaveChangesAsync();
            await ctx.DisposeAsync();
        }
    }
}

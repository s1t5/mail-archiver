using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services.Providers.Imap;
using MailArchiver.Tests.Infrastructure;
using MailArchiver.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace MailArchiver.Tests.Services;

/// <summary>
/// Integration tests for <see cref="ImapMailRestorer.OffloadEmailsAsync"/> against the
/// PostgreSQL test database. The offload is aimed at multi-thousand-mail migrations and
/// used to run two database round-trips per email: one lightweight lookup for the duplicate
/// check, and a second full load with attachments for the append. These tests fail the
/// offload before the first network hop, so no IMAP server is involved. They pin the
/// observable behaviour of the pre-connection path and, for the missing-target case, the
/// outcome accounting that the batched implementation must preserve.
/// </summary>
[Collection(TestDbFixture.CollectionName)]
public class ImapMailRestorerOffloadTests
{
    private readonly TestDbFixture _fixture;
    public ImapMailRestorerOffloadTests(TestDbFixture fixture) => _fixture = fixture;

    private static async Task<(MailAccount Source, List<ArchivedEmail> Emails, MailAccount Target)> SeedAsync(
        MailArchiverDbContext ctx, int emailCount)
    {
        var suffix = Guid.NewGuid().ToString("N").Substring(0, 12);

        var source = new MailAccount
        {
            Name = $"off-src-{suffix}",
            EmailAddress = $"src-{suffix}@test.local",
            Provider = ProviderType.IMAP,
            IsEnabled = true,
            LastSync = DateTime.UtcNow
        };
        var target = new MailAccount
        {
            Name = $"off-dst-{suffix}",
            EmailAddress = $"dst-{suffix}@test.local",
            // An unroutable server: the test asserts the failure before any connection attempt.
            ImapServer = "unreachable.test.invalid",
            ImapPort = 993,
            Username = "u",
            Password = "p",
            UseSSL = true,
            Provider = ProviderType.IMAP,
            IsEnabled = true,
            LastSync = DateTime.UtcNow
        };
        ctx.MailAccounts.AddRange(source, target);
        await ctx.SaveChangesAsync();

        var emails = new List<ArchivedEmail>();
        for (var i = 0; i < emailCount; i++)
        {
            emails.Add(new ArchivedEmail
            {
                MailAccountId = source.Id,
                MessageId = $"<{suffix}-{i}@test.local>",
                Subject = $"offload {i}",
                Body = $"body {i}",
                HtmlBody = string.Empty,
                From = "sender@test.local",
                To = "recipient@test.local",
                Cc = string.Empty,
                Bcc = string.Empty,
                SentDate = new DateTime(2026, 7, 12, 10, 0, 0, DateTimeKind.Unspecified).AddMinutes(i),
                ReceivedDate = DateTime.UtcNow,
                FolderName = "INBOX",
            });
        }
        ctx.ArchivedEmails.AddRange(emails);
        await ctx.SaveChangesAsync();
        return (source, emails, target);
    }

    [Fact]
    public async Task Offload_TargetNotFound_FailsEveryEmailWithoutQueryingThem()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        try
        {
            var (source, emails, _) = await SeedAsync(scope.Context, 5);
            var restorer = ServiceFactory.CreateImapMailRestorer(scope.Context);
            var criteria = new OffloadCriteria
            {
                SourceAccountId = source.Id,
                DryRun = false
            };

            var outcome = await restorer.OffloadEmailsAsync(
                emails.Select(e => e.Id).ToList(),
                targetAccountId: 999999, // does not exist
                baseFolderName: "Archive",
                preserveFolderStructure: true,
                criteria: criteria);

            Assert.Equal(emails.Count, outcome.Failed);
            Assert.Equal(0, outcome.Appended);
            Assert.Equal(0, outcome.SkippedAlreadyPresent);
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task Offload_ExcludesFoldersBeforeConnecting()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        try
        {
            var (source, emails, target) = await SeedAsync(scope.Context, 6);
            // Mark two of the emails as belonging to an excluded folder.
            var excluded = emails.Take(2).ToList();
            foreach (var e in excluded) e.FolderName = "Spam";
            await scope.Context.SaveChangesAsync();

            var restorer = ServiceFactory.CreateImapMailRestorer(scope.Context);
            var criteria = new OffloadCriteria
            {
                SourceAccountId = source.Id,
                // Exclude "Spam": those two must be counted and skipped before any connect.
                ExcludedSourceFolders = new List<string> { "Spam" },
                DryRun = false
            };

            var outcome = await restorer.OffloadEmailsAsync(
                emails.Select(e => e.Id).ToList(),
                target.Id,
                baseFolderName: "Archive",
                preserveFolderStructure: true,
                criteria: criteria);

            // The target is unreachable, so the remaining four are all failures, but the two
            // excluded ones must have been counted before any connection attempt.
            Assert.Equal(2, outcome.SkippedExcludedFolder);
            Assert.Equal(4, outcome.Failed);
            Assert.Equal(0, outcome.Appended);
        }
        finally { await scope.RollbackAsync(); }
    }
}

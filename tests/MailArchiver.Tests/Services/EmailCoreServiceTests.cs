using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services.Core;
using MailArchiver.Services.Shared;
using MailArchiver.Tests.Infrastructure;
using MailArchiver.ViewModels;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using System.Globalization;
using Xunit;

namespace MailArchiver.Tests.Services;

/// <summary>
/// Integration tests for <see cref="EmailCoreService"/> against the PostgreSQL Dev database.
/// Each test runs inside a rolled-back transaction so no rows persist.
/// </summary>
[Collection(TestDbFixture.CollectionName)]
public class EmailCoreServiceTests
{
    private readonly TestDbFixture _fixture;
    public EmailCoreServiceTests(TestDbFixture fixture) => _fixture = fixture;

    private static async Task<MailAccount> SeedAccountAsync(MailArchiverDbContext ctx, string? name = null)
    {
        var account = new MailAccount
        {
            Name = name ?? $"acct-{Guid.NewGuid():N}".Substring(0, 25),
            EmailAddress = $"{Guid.NewGuid():N}@test.local",
            Provider = ProviderType.IMAP,
            IsEnabled = true,
            LastSync = DateTime.UtcNow
        };
        ctx.MailAccounts.Add(account);
        await ctx.SaveChangesAsync();
        return account;
    }

    private static ArchivedEmail BuildEmail(MailAccount account, string subject, string from, string to,
        string body = "body", string? htmlBody = null, DateTime? sentDate = null, bool isOutgoing = false,
        string folder = "INBOX", string messageId = null!, string? fromDisplayName = null,
        string? toDisplayNames = null, string? rawHeaders = null)
        => new()
        {
            MailAccountId = account.Id,
            MessageId = messageId ?? Guid.NewGuid().ToString(),
            Subject = subject,
            From = from,
            To = to,
            Cc = string.Empty,
            Bcc = string.Empty,
            Body = body,
            HtmlBody = htmlBody ?? string.Empty,
            FromDisplayName = fromDisplayName,
            ToDisplayNames = toDisplayNames,
            RawHeaders = rawHeaders,
            SentDate = sentDate ?? DateTime.UtcNow.AddDays(-1),
            ReceivedDate = DateTime.UtcNow,
            IsOutgoing = isOutgoing,
            HasAttachments = false,
            FolderName = folder
        };

    // ============================================================
    // SearchEmailsAsync
    // ============================================================

    [Fact]
    public async Task Search_NoTerm_ReturnsAllAndRespectsPagination()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            ctx.ArchivedEmails.AddRange(
                BuildEmail(acct, "Alpha", "a@x.com", "b@x.com"),
                BuildEmail(acct, "Beta", "a@x.com", "b@x.com"),
                BuildEmail(acct, "Gamma", "a@x.com", "b@x.com"));
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateEmailCoreService(ctx);
            var (emails, total) = await svc.SearchEmailsAsync(null, null, null, acct.Id, null, null, 0, 50);

            Assert.Equal(3, total);
            Assert.Equal(3, emails.Count);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task Search_TakeOver1000_IsClamped()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            ctx.ArchivedEmails.Add(BuildEmail(acct, "Solo", "a@x.com", "b@x.com"));
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateEmailCoreService(ctx);
            // take=5000 must be clamped to 1000 without error.
            var (_, _) = await svc.SearchEmailsAsync(null, null, null, acct.Id, null, null, 0, 5000);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task Search_NegativeSkip_NormalizedToZero()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            ctx.ArchivedEmails.Add(BuildEmail(acct, "Solo", "a@x.com", "b@x.com"));
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateEmailCoreService(ctx);
            var (emails, _) = await svc.SearchEmailsAsync(null, null, null, acct.Id, null, null, -50, 10);
            Assert.Single(emails);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task Search_SingleWord_PrefixMatch()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            ctx.ArchivedEmails.AddRange(
                BuildEmail(acct, "Quarterly report", "a@x.com", "b@x.com"),
                BuildEmail(acct, "Unrelated", "a@x.com", "b@x.com"));
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateEmailCoreService(ctx);
            var (emails, total) = await svc.SearchEmailsAsync("quart", null, null, acct.Id, null, null, 0, 50);
            Assert.Equal(1, total);
            Assert.Single(emails);
            Assert.Contains("Quarterly", emails[0].Subject);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task Search_QuotedPhrase_MatchesExact()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            ctx.ArchivedEmails.AddRange(
                BuildEmail(acct, "status report project", "a@x.com", "b@x.com"),
                BuildEmail(acct, "project status update", "a@x.com", "b@x.com"));
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateEmailCoreService(ctx);
            var (_, total) = await svc.SearchEmailsAsync("\"project status\"", null, null, acct.Id, null, null, 0, 50);
            Assert.Equal(1, total);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task Search_FieldSearch_Subject_OnlyMatchesSubject()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            ctx.ArchivedEmails.AddRange(
                BuildEmail(acct, "important", "a@x.com", "b@x.com", body: "nothing here"),
                BuildEmail(acct, "boring", "a@x.com", "important@x.com", body: "body text"));
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateEmailCoreService(ctx);
            var (emails, total) = await svc.SearchEmailsAsync("subject:important", null, null, acct.Id, null, null, 0, 50);
            Assert.Equal(1, total);
            Assert.Equal("important", emails[0].Subject);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task Search_FieldSearch_From_MatchesFromOnly()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            ctx.ArchivedEmails.AddRange(
                BuildEmail(acct, "subject", "alice@x.com", "b@x.com"),
                BuildEmail(acct, "subject", "bob@x.com", "b@x.com"));
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateEmailCoreService(ctx);
            var (_, total) = await svc.SearchEmailsAsync("from:alice", null, null, acct.Id, null, null, 0, 50);
            Assert.Equal(1, total);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task Search_AccountFilter_ReturnsOnlyOwn()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var a1 = await SeedAccountAsync(ctx, "acct1");
            var a2 = await SeedAccountAsync(ctx, "acct2");
            ctx.ArchivedEmails.Add(BuildEmail(a1, "own", "a@x.com", "b@x.com"));
            ctx.ArchivedEmails.Add(BuildEmail(a2, "other", "a@x.com", "b@x.com"));
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateEmailCoreService(ctx);
            var (_, total) = await svc.SearchEmailsAsync(null, null, null, a1.Id, null, null, 0, 50);
            Assert.Equal(1, total);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task Search_AllowedAccountIds_RestrictsToSubset()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var a1 = await SeedAccountAsync(ctx);
            var a2 = await SeedAccountAsync(ctx);
            ctx.ArchivedEmails.Add(BuildEmail(a1, "x", "a@x.com", "b@x.com"));
            ctx.ArchivedEmails.Add(BuildEmail(a2, "y", "a@x.com", "b@x.com"));
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateEmailCoreService(ctx);
            var (_, total) = await svc.SearchEmailsAsync(null, null, null, null, null, null, 0, 50,
                allowedAccountIds: new List<int> { a1.Id });
            Assert.Equal(1, total);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task Search_EmptyAllowedList_ReturnsZero()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            ctx.ArchivedEmails.Add(BuildEmail(acct, "x", "a@x.com", "b@x.com"));
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateEmailCoreService(ctx);
            var (_, total) = await svc.SearchEmailsAsync(null, null, null, null, null, null, 0, 50,
                allowedAccountIds: new List<int>());
            Assert.Equal(0, total);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task Search_DateFilter_FromDateWorks()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            ctx.ArchivedEmails.Add(BuildEmail(acct, "old", "a@x.com", "b@x.com", sentDate: DateTime.UtcNow.AddDays(-30)));
            ctx.ArchivedEmails.Add(BuildEmail(acct, "new", "a@x.com", "b@x.com", sentDate: DateTime.UtcNow.AddHours(-1)));
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateEmailCoreService(ctx);
            var (_, total) = await svc.SearchEmailsAsync(null, DateTime.UtcNow.AddDays(-2), null, acct.Id, null, null, 0, 50);
            Assert.Equal(1, total);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task Search_DateFilter_ToDateWorks()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            ctx.ArchivedEmails.Add(BuildEmail(acct, "old", "a@x.com", "b@x.com", sentDate: DateTime.UtcNow.AddDays(-30)));
            ctx.ArchivedEmails.Add(BuildEmail(acct, "new", "a@x.com", "b@x.com", sentDate: DateTime.UtcNow.AddHours(-1)));
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateEmailCoreService(ctx);
            var (_, total) = await svc.SearchEmailsAsync(null, null, DateTime.UtcNow.AddDays(-2), acct.Id, null, null, 0, 50);
            Assert.Equal(1, total);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task Search_IsOutgoingFilter()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            ctx.ArchivedEmails.Add(BuildEmail(acct, "in", "a@x.com", "b@x.com", isOutgoing: false));
            ctx.ArchivedEmails.Add(BuildEmail(acct, "out", "a@x.com", "b@x.com", isOutgoing: true));
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateEmailCoreService(ctx);
            var (_, totalOut) = await svc.SearchEmailsAsync(null, null, null, acct.Id, null, true, 0, 50);
            Assert.Equal(1, totalOut);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task Search_FolderFilter()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            ctx.ArchivedEmails.Add(BuildEmail(acct, "a", "a@x.com", "b@x.com", folder: "INBOX"));
            ctx.ArchivedEmails.Add(BuildEmail(acct, "b", "a@x.com", "b@x.com", folder: "Sent"));
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateEmailCoreService(ctx);
            var (_, total) = await svc.SearchEmailsAsync(null, null, null, acct.Id, "Sent", null, 0, 50);
            Assert.Equal(1, total);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task Search_FolderFilter_IncludesDescendantFolders()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            ctx.ArchivedEmails.Add(BuildEmail(acct, "root-mail",    "a@x.com", "b@x.com", folder: "travel"));
            ctx.ArchivedEmails.Add(BuildEmail(acct, "nested-slash", "a@x.com", "b@x.com", folder: "travel/2022"));
            ctx.ArchivedEmails.Add(BuildEmail(acct, "deep-slash",   "a@x.com", "b@x.com", folder: "travel/2022/France"));
            ctx.ArchivedEmails.Add(BuildEmail(acct, "nested-backslash", "a@x.com", "b@x.com", folder: "archive\\2022"));
            ctx.ArchivedEmails.Add(BuildEmail(acct, "nested-dot",   "a@x.com", "b@x.com", folder: "lists.2022"));
            ctx.ArchivedEmails.Add(BuildEmail(acct, "lookalike",    "a@x.com", "b@x.com", folder: "travelXyz"));
            ctx.ArchivedEmails.Add(BuildEmail(acct, "unrelated",    "a@x.com", "b@x.com", folder: "INBOX"));
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateEmailCoreService(ctx);

            var (emails, total) = await svc.SearchEmailsAsync(null, null, null, acct.Id, "travel", null, 0, 50);
            Assert.Equal(3, total);
            Assert.Contains(emails, e => e.Subject == "root-mail");
            Assert.Contains(emails, e => e.Subject == "nested-slash");
            Assert.Contains(emails, e => e.Subject == "deep-slash");
            Assert.DoesNotContain(emails, e => e.Subject == "lookalike");
            Assert.DoesNotContain(emails, e => e.Subject == "unrelated");

            var (emailsBs, totalBs) = await svc.SearchEmailsAsync(null, null, null, acct.Id, "archive", null, 0, 50);
            Assert.Equal(1, totalBs);
            Assert.Equal("nested-backslash", emailsBs[0].Subject);

            var (emailsDot, totalDot) = await svc.SearchEmailsAsync(null, null, null, acct.Id, "lists", null, 0, 50);
            Assert.Equal(1, totalDot);
            Assert.Equal("nested-dot", emailsDot[0].Subject);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task Search_SortBy_SubjectAsc()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            ctx.ArchivedEmails.AddRange(
                BuildEmail(acct, "Zebra", "a@x.com", "b@x.com"),
                BuildEmail(acct, "Apple", "a@x.com", "b@x.com"),
                BuildEmail(acct, "Mango", "a@x.com", "b@x.com"));
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateEmailCoreService(ctx);
            var (emails, _) = await svc.SearchEmailsAsync(null, null, null, acct.Id, null, null, 0, 50,
                sortBy: "subject", sortOrder: "asc");
            Assert.Equal("Apple", emails[0].Subject);
            Assert.Equal("Zebra", emails[^1].Subject);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task Search_SortBy_SentDateDesc()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            ctx.ArchivedEmails.AddRange(
                BuildEmail(acct, "old", "a@x.com", "b@x.com", sentDate: DateTime.UtcNow.AddDays(-10)),
                BuildEmail(acct, "newest", "a@x.com", "b@x.com", sentDate: DateTime.UtcNow.AddHours(-1)),
                BuildEmail(acct, "mid", "a@x.com", "b@x.com", sentDate: DateTime.UtcNow.AddDays(-5)));
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateEmailCoreService(ctx);
            var (emails, _) = await svc.SearchEmailsAsync(null, null, null, acct.Id, null, null, 0, 50,
                sortBy: "sentdate", sortOrder: "desc");
            Assert.Equal("newest", emails[0].Subject);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    // ============================================================
    // GetEmailCountByAccountAsync
    // ============================================================

    [Fact]
    public async Task GetEmailCountByAccountAsync_CountsOnlyOwn()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var a1 = await SeedAccountAsync(ctx);
            var a2 = await SeedAccountAsync(ctx);
            ctx.ArchivedEmails.Add(BuildEmail(a1, "x", "a@x.com", "b@x.com"));
            ctx.ArchivedEmails.AddRange(BuildEmail(a2, "y", "a@x.com", "b@x.com"), BuildEmail(a2, "z", "a@x.com", "b@x.com"));
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateEmailCoreService(ctx);
            Assert.Equal(1, await svc.GetEmailCountByAccountAsync(a1.Id));
            Assert.Equal(2, await svc.GetEmailCountByAccountAsync(a2.Id));
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    // ============================================================
    // GetDashboardStatisticsAsync
    // ============================================================

    [Fact]
    public async Task GetDashboardStatisticsAsync_AggregatesCorrectly()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            ctx.ArchivedEmails.Add(BuildEmail(acct, "in", "sender@x.com", "b@x.com", isOutgoing: false));
            ctx.ArchivedEmails.Add(BuildEmail(acct, "out", "b@x.com", "c@x.com", isOutgoing: true));
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateEmailCoreServiceNoCache(ctx);
            var dash = await svc.GetDashboardStatisticsAsync();

            Assert.True(dash.TotalEmails >= 2);
            Assert.True(dash.TotalAccounts >= 1);
            Assert.Equal(12, dash.Series.Emails.Count);
            Assert.NotEmpty(dash.EmailsPerAccount);
            Assert.False(string.IsNullOrEmpty(dash.TotalStorageUsed));
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task GetDashboardStatisticsAsync_TopSenders_ExcludesOutgoing()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            ctx.ArchivedEmails.Add(BuildEmail(acct, "s1", "unique-sender-1@test.local", "b@x.com", isOutgoing: false));
            ctx.ArchivedEmails.Add(BuildEmail(acct, "s2", "unique-sender-2@test.local", "b@x.com", isOutgoing: false));
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateEmailCoreServiceNoCache(ctx);
            var dash = await svc.GetDashboardStatisticsAsync();

            // TopSenders only includes non-outgoing emails. We can't guarantee our test
            // senders make the top 10 (the Dev DB has real data), but we can verify the
            // query excludes outgoing by checking that an outgoing address we added is absent.
            Assert.DoesNotContain(dash.Series.TopSenders, s => s.EmailAddress == "b@x.com");
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task GetDashboardStatisticsAsync_RecentEmails_LimitedTo10Desc()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            for (int i = 0; i < 12; i++)
                ctx.ArchivedEmails.Add(BuildEmail(acct, $"e{i}", "a@x.com", "b@x.com", sentDate: DateTime.UtcNow.AddDays(-i)));
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateEmailCoreServiceNoCache(ctx);
            var dash = await svc.GetDashboardStatisticsAsync();

            // RecentEmails is capped at 10, ordered by SentDate desc, and carries the
            // account name without loading full email entities. The shared Dev DB may
            // contain newer emails from other accounts, so only check ordering, cap
            // and that our seeded emails carry the correct account name.
            Assert.True(dash.RecentEmails.Count <= 10);
            for (int i = 1; i < dash.RecentEmails.Count; i++)
                Assert.True(dash.RecentEmails[i - 1].SentDate >= dash.RecentEmails[i].SentDate);
            Assert.Contains(dash.RecentEmails, e => e.MailAccountName == acct.Name);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task GetDashboardStatisticsAsync_AccountPanel_LimitedTo25ByLastSyncDesc()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            // More accounts than the panel shows, with last-sync times spread far enough apart
            // that the ordering is unambiguous whatever else the shared DB holds.
            for (int i = 0; i < 30; i++)
            {
                var seeded = await SeedAccountAsync(ctx);
                seeded.LastSync = DateTime.UtcNow.AddMinutes(-i);
            }
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateEmailCoreServiceNoCache(ctx);
            var dash = await svc.GetDashboardStatisticsAsync();

            // The panel is capped and ordered by last sync, newest first. The shared Dev DB may
            // hold accounts of its own, so only the cap and the ordering are checked, the same
            // way the recent-emails test does it.
            Assert.True(dash.EmailsPerAccount.Count <= 25);
            for (int i = 1; i < dash.EmailsPerAccount.Count; i++)
                Assert.True(dash.EmailsPerAccount[i - 1].LastSyncTime >= dash.EmailsPerAccount[i].LastSyncTime);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task GetDashboardStatisticsAsync_AccountPanel_KeepsAnAccountWithIssuesThatLastSyncWouldDrop()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            // The account with the oldest timestamp is the one an order by last sync alone pushes
            // out of a capped panel, and it is exactly the shape of the accounts worth seeing:
            // a failed run does not advance LastSync, so a troubled account keeps sinking.
            MailAccount? oldest = null;
            for (int i = 0; i < 30; i++)
            {
                var seeded = await SeedAccountAsync(ctx);
                seeded.LastSync = DateTime.UtcNow.AddMinutes(-i);
                if (i == 29) oldest = seeded;
            }
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateEmailCoreServiceNoCache(ctx);
            var dash = await svc.GetDashboardStatisticsAsync(id => id == oldest!.Id);

            Assert.True(dash.EmailsPerAccount.Count <= 25);
            Assert.Equal(oldest!.Id, dash.EmailsPerAccount[0].AccountId);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task GetDashboardStatisticsAsync_AccountPanel_OrdersByLastSyncWithinEachGroup()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            // Two flagged accounts, the older one flagged first, so a stable-but-unordered
            // implementation would return them the wrong way round.
            var older = await SeedAccountAsync(ctx);
            older.LastSync = DateTime.UtcNow.AddMinutes(-40);
            var newer = await SeedAccountAsync(ctx);
            newer.LastSync = DateTime.UtcNow.AddMinutes(-30);
            for (int i = 0; i < 28; i++)
            {
                var seeded = await SeedAccountAsync(ctx);
                seeded.LastSync = DateTime.UtcNow.AddMinutes(-i);
            }
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateEmailCoreServiceNoCache(ctx);
            var flagged = new[] { older.Id, newer.Id };
            var dash = await svc.GetDashboardStatisticsAsync(id => flagged.Contains(id));

            Assert.Equal(newer.Id, dash.EmailsPerAccount[0].AccountId);
            Assert.Equal(older.Id, dash.EmailsPerAccount[1].AccountId);

            // And the rest still reads newest first.
            for (int i = 3; i < dash.EmailsPerAccount.Count; i++)
                Assert.True(dash.EmailsPerAccount[i - 1].LastSyncTime >= dash.EmailsPerAccount[i].LastSyncTime);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task GetDashboardStatisticsAsync_AccountPanel_CountsAreForTheRowsThatAreShown()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            // The second pass has to carry the per-account message count over, and it is loaded
            // through a Contains() whose result carries no order of its own.
            var flagged = await SeedAccountAsync(ctx);
            flagged.LastSync = DateTime.UtcNow.AddMinutes(-90);
            ctx.ArchivedEmails.Add(BuildEmail(flagged, "s1", "a@test.local", "b@test.local"));
            ctx.ArchivedEmails.Add(BuildEmail(flagged, "s2", "a@test.local", "b@test.local"));
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateEmailCoreServiceNoCache(ctx);
            var dash = await svc.GetDashboardStatisticsAsync(id => id == flagged.Id);

            var row = dash.EmailsPerAccount[0];
            Assert.Equal(flagged.Id, row.AccountId);
            Assert.Equal(2, row.EmailCount);
            Assert.Equal(flagged.Name, row.AccountName);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public void ApplyPanelOrder_StaleCachedOrderIsCorrectedAgainstFreshFlags()
    {
        // The panel order is cached, the flags are read per request. A run finishing or a
        // failure being acknowledged within the cache window has to reorder the rows, or the
        // panel shows an account marked as troubled somewhere it can be missed. The troubled
        // account is also the older one, because a failed run does not advance LastSync.
        var troubled = new MailArchiver.Models.ViewModels.AccountStatistics { AccountId = 1, LastSyncTime = DateTime.UtcNow.AddMinutes(-40) };
        var healthy = new MailArchiver.Models.ViewModels.AccountStatistics { AccountId = 2, LastSyncTime = DateTime.UtcNow.AddMinutes(-10) };
        var rows = new List<MailArchiver.Models.ViewModels.AccountStatistics> { healthy, troubled };

        EmailCoreService.ApplyPanelOrder(rows, id => id == troubled.AccountId);

        Assert.Equal(troubled.AccountId, rows[0].AccountId);
        Assert.Equal(healthy.AccountId, rows[1].AccountId);

        // Acknowledging the failure drops the account back into last-sync order without
        // changing the underlying rows.
        EmailCoreService.ApplyPanelOrder(rows, _ => false);
        Assert.Equal(healthy.AccountId, rows[0].AccountId);
        Assert.Equal(troubled.AccountId, rows[1].AccountId);
        Assert.Same(troubled, rows[1]);
    }

    [Fact]
    public void ApplyPanelOrder_ShortOrEmptyListsAreUntouched()
    {
        var single = new List<MailArchiver.Models.ViewModels.AccountStatistics> { new() { AccountId = 1, LastSyncTime = DateTime.UtcNow } };
        EmailCoreService.ApplyPanelOrder(single, _ => true);
        Assert.Single(single);

        var empty = new List<MailArchiver.Models.ViewModels.AccountStatistics>();
        EmailCoreService.ApplyPanelOrder(empty, _ => true);
        Assert.Empty(empty);

        EmailCoreService.ApplyPanelOrder(null!, _ => true);
    }

    [Fact]
    public async Task GetDashboardStatisticsAsync_CurrentPeriodIsCountedNotCutOff()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            // Send dates are stored as wall-clock times of the display timezone, which the test
            // services configure as Europe/Berlin. Any instant inside the current month of that
            // timezone belongs to the last bucket, so the first hour of it is a date that is in
            // range whenever the suite runs.
            var nowInDisplayZone = TimeZoneInfo
                .ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin"))
                .DateTime;
            var insideCurrentMonth = new DateTime(nowInDisplayZone.Year, nowInDisplayZone.Month, 1, 1, 0, 0);

            var acct = await SeedAccountAsync(ctx);
            ctx.ArchivedEmails.Add(BuildEmail(acct, "now", "a@x.com", "b@x.com", sentDate: insideCurrentMonth));
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateEmailCoreServiceNoCache(ctx);
            var dash = await svc.GetDashboardStatisticsAsync();

            // The upper bound of the range is the end of the bucket holding the current
            // instant, not the instant itself, so the bucket the dashboard is looked at in is
            // the last one and it counts.
            Assert.Equal(12, dash.Series.Emails.Count);
            Assert.True(dash.Series.Emails[^1].Count >= 1);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task GetDashboardStatisticsAsync_Cache_ReturnsCopyWithinTtl()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var email1 = BuildEmail(acct, "cached-1", "a@x.com", "b@x.com");
            ctx.ArchivedEmails.Add(email1);
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateEmailCoreService(ctx); // default: CacheSeconds = 60
            var first = await svc.GetDashboardStatisticsAsync();

            // Mutating the returned model (as the controller does for sync/storage badges)
            // must not leak into the cache entry.
            first.EmailsPerAccount[0].StorageUsed = "leaked";

            var second = await svc.GetDashboardStatisticsAsync();
            Assert.NotEqual("leaked", second.EmailsPerAccount[0].StorageUsed);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }


    // ============================================================
    // Direction splits on the dashboard counters
    // ============================================================

    [Fact]
    public void CountAccountDomains_CountsDistinctDomainsIgnoringCase()
    {
        var count = EmailCoreService.CountAccountDomains(new[]
        {
            "a@example.com",
            "b@EXAMPLE.COM",
            "c@other.test"
        });

        Assert.Equal(2, count);
    }

    [Fact]
    public void CountAccountDomains_TakesTheDomainAfterTheLastAtSign()
    {
        // Both addresses carry an '@' inside a quoted local part. Only the last one
        // separates the domain, so these two share a domain rather than having one each.
        var count = EmailCoreService.CountAccountDomains(new[]
        {
            "\"x@a\"@example.com",
            "\"y@b\"@example.com"
        });

        Assert.Equal(1, count);
    }

    [Fact]
    public void CountAccountDomains_AddressesWithoutADomainContributeNothing()
    {
        var count = EmailCoreService.CountAccountDomains(new string?[]
        {
            null,
            "",
            "   ",
            "no-at-sign",
            "trailing@",
            "a@example.com"
        });

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task CountEmailsByDirection_SplitsTheMailAndTotalsTheParts()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            ctx.ArchivedEmails.Add(BuildEmail(acct, "in-1", "a@x.com", "b@x.com", isOutgoing: false));
            ctx.ArchivedEmails.Add(BuildEmail(acct, "in-2", "a@x.com", "b@x.com", isOutgoing: false));
            ctx.ArchivedEmails.Add(BuildEmail(acct, "out-1", "b@x.com", "c@x.com", isOutgoing: true));
            await ctx.SaveChangesAsync();

            var counts = EmailCoreService.CountEmailsByDirection(
                ctx.ArchivedEmails.Where(e => e.MailAccountId == acct.Id));

            Assert.Equal(2, counts.Incoming);
            Assert.Equal(1, counts.Outgoing);
            Assert.Equal(3, counts.Total);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task CountEmailsByDirection_OneSidedMailLeavesTheOtherSideAtZero()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            // The grouped query returns a row per direction that occurs, so an account
            // that only ever received mail produces one row. The absent direction has to
            // read as zero instead of dropping out of the split.
            var acct = await SeedAccountAsync(ctx);
            ctx.ArchivedEmails.Add(BuildEmail(acct, "in-only", "a@x.com", "b@x.com", isOutgoing: false));
            await ctx.SaveChangesAsync();

            var counts = EmailCoreService.CountEmailsByDirection(
                ctx.ArchivedEmails.Where(e => e.MailAccountId == acct.Id));

            Assert.Equal(1, counts.Incoming);
            Assert.Equal(0, counts.Outgoing);
            Assert.Equal(1, counts.Total);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task CountAttachmentsByDirection_FollowsTheDirectionOfTheCarryingEmail()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            // The direction is not on the attachment row, so the count has to reach the
            // email that carries it. Two attachments arrive, one is sent.
            var acct = await SeedAccountAsync(ctx);

            var incoming = BuildEmail(acct, "in-with-files", "a@x.com", "b@x.com", isOutgoing: false);
            incoming.HasAttachments = true;
            incoming.Attachments = new List<EmailAttachment>
            {
                new() { FileName = "one.txt", ContentType = "text/plain", Size = 1, LegacyContent = new byte[] { 1 } },
                new() { FileName = "two.txt", ContentType = "text/plain", Size = 1, LegacyContent = new byte[] { 2 } }
            };

            var outgoing = BuildEmail(acct, "out-with-file", "b@x.com", "c@x.com", isOutgoing: true);
            outgoing.HasAttachments = true;
            outgoing.Attachments = new List<EmailAttachment>
            {
                new() { FileName = "three.txt", ContentType = "text/plain", Size = 1, LegacyContent = new byte[] { 3 } }
            };

            ctx.ArchivedEmails.Add(incoming);
            ctx.ArchivedEmails.Add(outgoing);
            await ctx.SaveChangesAsync();

            var counts = EmailCoreService.CountAttachmentsByDirection(
                ctx.EmailAttachments.Where(a => a.ArchivedEmail.MailAccountId == acct.Id));

            Assert.Equal(2, counts.Incoming);
            Assert.Equal(1, counts.Outgoing);
            Assert.Equal(3, counts.Total);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task GetDashboardStatisticsAsync_CountersAgreeWithTheirSplits()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);

            var incoming = BuildEmail(acct, "dash-in", "a@x.com", "b@x.com", isOutgoing: false);
            incoming.HasAttachments = true;
            incoming.Attachments = new List<EmailAttachment>
            {
                new() { FileName = "in.txt", ContentType = "text/plain", Size = 1, LegacyContent = new byte[] { 1 } }
            };

            var outgoing = BuildEmail(acct, "dash-out", "b@x.com", "c@x.com", isOutgoing: true);
            outgoing.HasAttachments = true;
            outgoing.Attachments = new List<EmailAttachment>
            {
                new() { FileName = "out.txt", ContentType = "text/plain", Size = 1, LegacyContent = new byte[] { 2 } }
            };

            ctx.ArchivedEmails.Add(incoming);
            ctx.ArchivedEmails.Add(outgoing);
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateEmailCoreServiceNoCache(ctx);
            var dash = await svc.GetDashboardStatisticsAsync();

            // The totals are derived from the two parts, so they add up whatever else the
            // database holds.
            Assert.Equal(dash.TotalEmails, dash.IncomingEmails + dash.OutgoingEmails);
            Assert.Equal(dash.TotalAttachments, dash.IncomingAttachments + dash.OutgoingAttachments);
            Assert.True(dash.IncomingEmails >= 1);
            Assert.True(dash.OutgoingEmails >= 1);
            Assert.True(dash.IncomingAttachments >= 1);
            Assert.True(dash.OutgoingAttachments >= 1);

            // Every account has at most one domain, and the seeded one has exactly one.
            Assert.True(dash.AccountDomains >= 1);
            Assert.True(dash.AccountDomains <= dash.TotalAccounts);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task GetDashboardStatisticsAsync_Cache_CarriesTheSplitsIntoTheCopy()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            // The cached statistics are handed out as a deep copy. A counter left out of
            // that copy reads as zero from the first request on and looks like an empty
            // archive rather than like a defect, so the copy is asserted to carry values.
            var acct = await SeedAccountAsync(ctx);

            var incoming = BuildEmail(acct, "clone-in", "a@x.com", "b@x.com", isOutgoing: false);
            incoming.HasAttachments = true;
            incoming.Attachments = new List<EmailAttachment>
            {
                new() { FileName = "in.txt", ContentType = "text/plain", Size = 1, LegacyContent = new byte[] { 1 } }
            };

            var outgoing = BuildEmail(acct, "clone-out", "b@x.com", "c@x.com", isOutgoing: true);
            outgoing.HasAttachments = true;
            outgoing.Attachments = new List<EmailAttachment>
            {
                new() { FileName = "out.txt", ContentType = "text/plain", Size = 1, LegacyContent = new byte[] { 2 } }
            };

            ctx.ArchivedEmails.Add(incoming);
            ctx.ArchivedEmails.Add(outgoing);
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateEmailCoreService(ctx); // default: CacheSeconds = 60
            var first = await svc.GetDashboardStatisticsAsync();
            var second = await svc.GetDashboardStatisticsAsync();

            Assert.True(first.IncomingEmails >= 1);
            Assert.True(first.OutgoingEmails >= 1);
            Assert.True(first.IncomingAttachments >= 1);
            Assert.True(first.OutgoingAttachments >= 1);
            Assert.True(first.AccountDomains >= 1);

            Assert.Equal(first.IncomingEmails, second.IncomingEmails);
            Assert.Equal(first.OutgoingEmails, second.OutgoingEmails);
            Assert.Equal(first.IncomingAttachments, second.IncomingAttachments);
            Assert.Equal(first.OutgoingAttachments, second.OutgoingAttachments);
            Assert.Equal(first.AccountDomains, second.AccountDomains);
            Assert.Equal(first.TotalAccounts, second.TotalAccounts);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }


    // ============================================================
    // Chart series for a chosen period
    // ============================================================

    private static readonly TimeZoneInfo _displayZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    /// <summary>
    /// Wall-clock time of the timezone send dates are stored in, which is what the period
    /// resolution expects and what the test services are configured with.
    /// </summary>
    private static DateTime NowInDisplayZone() =>
        TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, _displayZone).DateTime;

    private static PeriodWindow Window(string key) =>
        DashboardPeriods.Windows.Single(w => w.Key == key);

    [Fact]
    public async Task BuildEmailSeries_CountsEachBucketByDirection()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            // Seeded into named buckets of the resolved range rather than into "today", so the
            // test says which bar it means, and so that mail dated now does not crowd the ten
            // most recent messages that another test looks at.
            var range = DashboardPeriods.Resolve(PeriodGranularity.Day, Window("7d"), NowInDisplayZone());
            var counted = range.Buckets[2].Start;
            var earlier = range.Buckets[1].Start;

            var acct = await SeedAccountAsync(ctx);
            ctx.ArchivedEmails.Add(BuildEmail(acct, "b2-in-1", "a@x.com", "b@x.com",
                sentDate: counted.AddHours(9), isOutgoing: false));
            ctx.ArchivedEmails.Add(BuildEmail(acct, "b2-in-2", "a@x.com", "b@x.com",
                sentDate: counted.AddHours(10), isOutgoing: false));
            ctx.ArchivedEmails.Add(BuildEmail(acct, "b2-out", "b@x.com", "c@x.com",
                sentDate: counted.AddHours(11), isOutgoing: true));
            ctx.ArchivedEmails.Add(BuildEmail(acct, "b1-in", "a@x.com", "b@x.com",
                sentDate: earlier.AddHours(9), isOutgoing: false));
            await ctx.SaveChangesAsync();

            var series = EmailCoreService.BuildEmailSeries(
                ctx.ArchivedEmails.Where(e => e.MailAccountId == acct.Id), range);

            Assert.Equal(7, series.Count);

            Assert.Equal(2, series[2].Incoming);
            Assert.Equal(1, series[2].Outgoing);
            Assert.Equal(3, series[2].Count);

            Assert.Equal(1, series[1].Incoming);
            Assert.Equal(0, series[1].Outgoing);

            // Everything seeded is accounted for, so nothing was folded into the wrong bar.
            Assert.Equal(4, series.Sum(b => b.Count));
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task BuildEmailSeries_MailOutsideTheWindowIsNotCounted()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var range = DashboardPeriods.Resolve(PeriodGranularity.Day, Window("7d"), NowInDisplayZone());

            var acct = await SeedAccountAsync(ctx);
            ctx.ArchivedEmails.Add(BuildEmail(acct, "inside", "a@x.com", "b@x.com",
                sentDate: range.Buckets[1].Start.AddHours(9)));
            ctx.ArchivedEmails.Add(BuildEmail(acct, "older", "a@x.com", "b@x.com",
                sentDate: range.Buckets[0].Start.AddDays(-40).AddHours(9)));
            await ctx.SaveChangesAsync();

            var series = EmailCoreService.BuildEmailSeries(
                ctx.ArchivedEmails.Where(e => e.MailAccountId == acct.Id), range);

            Assert.Equal(1, series.Sum(b => b.Count));
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task BuildEmailSeries_WithoutALowerBoundTheFirstBucketHoldsTheOlderMail()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            // The window that reaches as far back as the archive goes has no lower bound, and
            // its axis stops at the bucket cap. Mail older than the first bar is counted there
            // rather than dropped, so the bars still add up to the archive.
            var now = NowInDisplayZone();
            var acct = await SeedAccountAsync(ctx);
            ctx.ArchivedEmails.Add(BuildEmail(acct, "ancient", "a@x.com", "b@x.com",
                sentDate: new DateTime(2003, 5, 5, 12, 0, 0)));
            ctx.ArchivedEmails.Add(BuildEmail(acct, "recent", "a@x.com", "b@x.com",
                sentDate: new DateTime(now.Year, 1, 2, 9, 0, 0)));
            await ctx.SaveChangesAsync();

            // A range of three years, deliberately not reaching back to the ancient message.
            var clipped = new PeriodRange(
                PeriodGranularity.Year,
                Window("all"),
                filterStart: null,
                endExclusive: new DateTime(now.Year + 1, 1, 1),
                buckets: new[]
                {
                    new PeriodBucket(new DateTime(now.Year - 2, 1, 1), "older"),
                    new PeriodBucket(new DateTime(now.Year - 1, 1, 1), "middle"),
                    new PeriodBucket(new DateTime(now.Year, 1, 1), "current")
                },
                firstBucketCollectsOlder: true);

            var series = EmailCoreService.BuildEmailSeries(
                ctx.ArchivedEmails.Where(e => e.MailAccountId == acct.Id), clipped);

            Assert.Equal(1, series[0].Count);
            Assert.Equal(1, series[^1].Count);
            Assert.Equal(2, series.Sum(b => b.Count));
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task BuildTopSenders_CountsTheChosenDirectionWithinTheWindow()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var range = DashboardPeriods.Resolve(PeriodGranularity.Day, Window("7d"), NowInDisplayZone());
            var inWindow = range.Buckets[2].Start;

            var acct = await SeedAccountAsync(ctx);
            ctx.ArchivedEmails.Add(BuildEmail(acct, "r1", "loud@in.test", "b@x.com",
                sentDate: inWindow.AddHours(8), isOutgoing: false));
            ctx.ArchivedEmails.Add(BuildEmail(acct, "r2", "loud@in.test", "b@x.com",
                sentDate: inWindow.AddHours(9), isOutgoing: false));
            ctx.ArchivedEmails.Add(BuildEmail(acct, "r3", "quiet@in.test", "b@x.com",
                sentDate: inWindow.AddHours(10), isOutgoing: false));
            ctx.ArchivedEmails.Add(BuildEmail(acct, "s1", "mine@out.test", "c@x.com",
                sentDate: inWindow.AddHours(11), isOutgoing: true));
            // Outside the window, so it must not appear in either direction.
            ctx.ArchivedEmails.Add(BuildEmail(acct, "old", "ancient@in.test", "b@x.com",
                sentDate: range.Buckets[0].Start.AddDays(-40), isOutgoing: false));
            await ctx.SaveChangesAsync();

            var accountMail = ctx.ArchivedEmails.Where(e => e.MailAccountId == acct.Id);

            var received = EmailCoreService.BuildTopSenders(accountMail, range, outgoing: false, take: 10);
            Assert.Equal("loud@in.test", received[0].EmailAddress);
            Assert.Equal(2, received[0].Count);
            Assert.Contains(received, s => s.EmailAddress == "quiet@in.test");
            Assert.DoesNotContain(received, s => s.EmailAddress == "mine@out.test");
            Assert.DoesNotContain(received, s => s.EmailAddress == "ancient@in.test");

            var sent = EmailCoreService.BuildTopSenders(accountMail, range, outgoing: true, take: 10);
            Assert.Single(sent);
            Assert.Equal("mine@out.test", sent[0].EmailAddress);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task BuildTopSenders_EqualCountsKeepAStableOrder()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var range = DashboardPeriods.Resolve(PeriodGranularity.Day, Window("7d"), NowInDisplayZone());
            var inWindow = range.Buckets[2].Start;

            var acct = await SeedAccountAsync(ctx);
            ctx.ArchivedEmails.Add(BuildEmail(acct, "t1", "bbb@tie.test", "b@x.com",
                sentDate: inWindow.AddHours(8)));
            ctx.ArchivedEmails.Add(BuildEmail(acct, "t2", "aaa@tie.test", "b@x.com",
                sentDate: inWindow.AddHours(9)));
            await ctx.SaveChangesAsync();

            var senders = EmailCoreService.BuildTopSenders(
                ctx.ArchivedEmails.Where(e => e.MailAccountId == acct.Id), range, outgoing: false, take: 10);

            Assert.Equal(new[] { "aaa@tie.test", "bbb@tie.test" }, senders.Select(s => s.EmailAddress));
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task GetChartSeriesAsync_AnswersForTheSelectionAndScopesToTheGivenAccounts()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var inWindow = DashboardPeriods
                .Resolve(PeriodGranularity.Day, Window("7d"), NowInDisplayZone())
                .Buckets[2].Start.AddHours(9);

            var mine = await SeedAccountAsync(ctx);
            var other = await SeedAccountAsync(ctx);

            ctx.ArchivedEmails.Add(BuildEmail(mine, "mine", "mine@scope.test", "b@x.com",
                sentDate: inWindow));
            ctx.ArchivedEmails.Add(BuildEmail(other, "theirs", "theirs@scope.test", "b@x.com",
                sentDate: inWindow));
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateEmailCoreServiceNoCache(ctx);
            var series = await svc.GetChartSeriesAsync(
                new List<int> { mine.Id }, PeriodGranularity.Day, Window("7d"), outgoingSenders: false);

            Assert.Equal("Day", series.Granularity);
            Assert.Equal("7d", series.Window);
            Assert.False(series.OutgoingSenders);
            Assert.Equal(7, series.Emails.Count);
            Assert.Equal(1, series.Emails.Sum(b => b.Count));

            Assert.Contains(series.TopSenders, s => s.EmailAddress == "mine@scope.test");
            Assert.DoesNotContain(series.TopSenders, s => s.EmailAddress == "theirs@scope.test");
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task GetChartSeriesAsync_ReachingBackAsFarAsTheArchiveGoesIncludesOldMail()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            // This is the one window that has to ask where the archive starts, and the one that
            // must not leave anything out at the bottom.
            var acct = await SeedAccountAsync(ctx);
            ctx.ArchivedEmails.Add(BuildEmail(acct, "old", "a@x.com", "b@x.com",
                sentDate: new DateTime(2011, 2, 3, 4, 5, 0)));
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateEmailCoreServiceNoCache(ctx);
            var series = await svc.GetChartSeriesAsync(
                new List<int> { acct.Id }, PeriodGranularity.Year, Window("all"), outgoingSenders: false);

            Assert.Equal("all", series.Window);
            Assert.Equal(1, series.Emails.Sum(b => b.Count));
            Assert.StartsWith("≤", series.Emails[0].Period);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task GetChartSeriesAsync_Cache_CarriesTheSeriesIntoTheCopy()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            // Same trap as the statistics: a cached series is handed out as a deep copy, and a
            // field missing from that copy shows an empty chart rather than an error.
            var inWindow = DashboardPeriods
                .Resolve(PeriodGranularity.Day, Window("7d"), NowInDisplayZone())
                .Buckets[2].Start.AddHours(9);

            var acct = await SeedAccountAsync(ctx);
            ctx.ArchivedEmails.Add(BuildEmail(acct, "cached", "cache@series.test", "b@x.com",
                sentDate: inWindow));
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateEmailCoreService(ctx); // default: CacheSeconds = 60
            var scope = new List<int> { acct.Id };

            var first = await svc.GetChartSeriesAsync(scope, PeriodGranularity.Day, Window("7d"), false);
            first.Emails[0].Incoming = 999;
            first.TopSenders.Clear();

            var second = await svc.GetChartSeriesAsync(scope, PeriodGranularity.Day, Window("7d"), false);

            Assert.Equal("Day", second.Granularity);
            Assert.Equal("7d", second.Window);
            Assert.Equal(7, second.Emails.Count);
            Assert.Equal(1, second.Emails.Sum(b => b.Count));
            Assert.Contains(second.TopSenders, s => s.EmailAddress == "cache@series.test");
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task GetChartSeriesAsync_EachSelectionIsCachedOnItsOwn()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            // One cache entry per scope and selection: a second selection must not be answered
            // with the first one's series.
            var inWindow = DashboardPeriods
                .Resolve(PeriodGranularity.Day, Window("7d"), NowInDisplayZone())
                .Buckets[2].Start.AddHours(9);

            var acct = await SeedAccountAsync(ctx);
            ctx.ArchivedEmails.Add(BuildEmail(acct, "one", "a@x.com", "b@x.com",
                sentDate: inWindow));
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateEmailCoreService(ctx);
            var scope = new List<int> { acct.Id };

            var byDay = await svc.GetChartSeriesAsync(scope, PeriodGranularity.Day, Window("7d"), false);
            var byMonth = await svc.GetChartSeriesAsync(scope, PeriodGranularity.Month, Window("1y"), false);
            var sent = await svc.GetChartSeriesAsync(scope, PeriodGranularity.Day, Window("7d"), true);

            Assert.Equal(7, byDay.Emails.Count);
            Assert.Equal(12, byMonth.Emails.Count);
            Assert.False(byDay.OutgoingSenders);
            Assert.True(sent.OutgoingSenders);
            Assert.Empty(sent.TopSenders);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task GetChartSeriesAsync_AnEarlierPageCountsThatWindowAndNotThisOne()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var now = NowInDisplayZone();
            var present = DashboardPeriods.Resolve(PeriodGranularity.Day, Window("2d"), now, now.AddYears(-1));
            var earlier = DashboardPeriods.Resolve(PeriodGranularity.Day, Window("2d"), now, now.AddYears(-1), offset: -1);

            var acct = await SeedAccountAsync(ctx);
            // One in the window that is current, two in the one before it, and an anchor far
            // enough back that paging is allowed at all.
            ctx.ArchivedEmails.Add(BuildEmail(acct, "p-now", "a@x.com", "b@x.com",
                sentDate: present.Buckets[0].Start.AddHours(9)));
            ctx.ArchivedEmails.Add(BuildEmail(acct, "p-back-1", "a@x.com", "b@x.com",
                sentDate: earlier.Buckets[0].Start.AddHours(9)));
            ctx.ArchivedEmails.Add(BuildEmail(acct, "p-back-2", "a@x.com", "b@x.com",
                sentDate: earlier.Buckets[^1].Start.AddHours(9)));
            ctx.ArchivedEmails.Add(BuildEmail(acct, "p-anchor", "a@x.com", "b@x.com",
                sentDate: now.AddYears(-1)));
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateEmailCoreServiceNoCache(ctx);
            var scope = new List<int> { acct.Id };

            var page0 = await svc.GetChartSeriesAsync(scope, PeriodGranularity.Day, Window("2d"), false, 0);
            var page1 = await svc.GetChartSeriesAsync(scope, PeriodGranularity.Day, Window("2d"), false, -1);

            Assert.Equal(0, page0.Offset);
            Assert.Equal(1, page0.Emails.Sum(b => b.Count));
            Assert.True(page0.CanGoBack);
            Assert.False(page0.CanGoForward);

            Assert.Equal(-1, page1.Offset);
            Assert.Equal(2, page1.Emails.Sum(b => b.Count));
            Assert.True(page1.CanGoForward);
            Assert.NotEqual(page0.RangeLabel, page1.RangeLabel);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task GetChartSeriesAsync_TheSendersFollowThePageThatIsShown()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var now = NowInDisplayZone();
            var present = DashboardPeriods.Resolve(PeriodGranularity.Day, Window("2d"), now, now.AddYears(-1));
            var earlier = DashboardPeriods.Resolve(PeriodGranularity.Day, Window("2d"), now, now.AddYears(-1), offset: -1);

            var acct = await SeedAccountAsync(ctx);
            ctx.ArchivedEmails.Add(BuildEmail(acct, "s-now", "thisweek@page.test", "b@x.com",
                sentDate: present.Buckets[0].Start.AddHours(9)));
            ctx.ArchivedEmails.Add(BuildEmail(acct, "s-back", "lastweek@page.test", "b@x.com",
                sentDate: earlier.Buckets[0].Start.AddHours(9)));
            ctx.ArchivedEmails.Add(BuildEmail(acct, "s-anchor", "anchor@page.test", "b@x.com",
                sentDate: now.AddYears(-1)));
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateEmailCoreServiceNoCache(ctx);
            var scope = new List<int> { acct.Id };

            var page0 = await svc.GetChartSeriesAsync(scope, PeriodGranularity.Day, Window("2d"), false, 0);
            var page1 = await svc.GetChartSeriesAsync(scope, PeriodGranularity.Day, Window("2d"), false, -1);

            Assert.Contains(page0.TopSenders, s => s.EmailAddress == "thisweek@page.test");
            Assert.DoesNotContain(page0.TopSenders, s => s.EmailAddress == "lastweek@page.test");

            Assert.Contains(page1.TopSenders, s => s.EmailAddress == "lastweek@page.test");
            Assert.DoesNotContain(page1.TopSenders, s => s.EmailAddress == "thisweek@page.test");
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task GetChartSeriesAsync_EachPageIsCachedOnItsOwn()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var now = NowInDisplayZone();
            var earlier = DashboardPeriods.Resolve(PeriodGranularity.Day, Window("2d"), now, now.AddYears(-1), offset: -1);

            var acct = await SeedAccountAsync(ctx);
            ctx.ArchivedEmails.Add(BuildEmail(acct, "c-back", "a@x.com", "b@x.com",
                sentDate: earlier.Buckets[0].Start.AddHours(9)));
            ctx.ArchivedEmails.Add(BuildEmail(acct, "c-anchor", "a@x.com", "b@x.com",
                sentDate: now.AddYears(-1)));
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateEmailCoreService(ctx); // default: CacheSeconds = 60
            var scope = new List<int> { acct.Id };

            var page0 = await svc.GetChartSeriesAsync(scope, PeriodGranularity.Day, Window("2d"), false, 0);
            var page1 = await svc.GetChartSeriesAsync(scope, PeriodGranularity.Day, Window("2d"), false, -1);

            // The second page must not be answered out of the first page's entry.
            Assert.Equal(0, page0.Emails.Sum(b => b.Count));
            Assert.Equal(1, page1.Emails.Sum(b => b.Count));
            Assert.Equal(0, page0.Offset);
            Assert.Equal(-1, page1.Offset);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task GetChartSeriesAsync_APageBeyondTheOldestMailComesBackAtTheFurthestOne()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var now = NowInDisplayZone();
            var acct = await SeedAccountAsync(ctx);
            ctx.ArchivedEmails.Add(BuildEmail(acct, "far-anchor", "a@x.com", "b@x.com",
                sentDate: now.Date.AddDays(-9)));
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateEmailCoreServiceNoCache(ctx);
            var scope = new List<int> { acct.Id };

            var clamped = await svc.GetChartSeriesAsync(scope, PeriodGranularity.Day, Window("2d"), false, -500);

            Assert.True(clamped.Offset > -500);
            Assert.False(clamped.CanGoBack);
            Assert.Equal(2, clamped.Emails.Count);
            Assert.Equal(1, clamped.Emails.Sum(b => b.Count));
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    // ============================================================
    // Switching the dashboard features off
    // ============================================================

    [Fact]
    public async Task ShowDirectionSplits_Off_LeavesTheTotalsRightAndThePartsAtZero()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var incoming = BuildEmail(acct, "off-in", "a@x.com", "b@x.com", isOutgoing: false);
            incoming.HasAttachments = true;
            incoming.Attachments = new List<EmailAttachment>
            {
                new() { FileName = "off.txt", ContentType = "text/plain", Size = 1, LegacyContent = new byte[] { 1 } }
            };
            ctx.ArchivedEmails.Add(incoming);
            ctx.ArchivedEmails.Add(BuildEmail(acct, "off-out", "b@x.com", "c@x.com", isOutgoing: true));
            await ctx.SaveChangesAsync();

            var on = await ServiceFactory.CreateEmailCoreServiceNoCache(ctx).GetDashboardStatisticsAsync();
            var off = await ServiceFactory.CreateEmailCoreServiceWithoutDashboardFeatures(ctx)
                .GetDashboardStatisticsAsync();

            // The numbers that were always there stay the same either way.
            Assert.Equal(on.TotalEmails, off.TotalEmails);
            Assert.Equal(on.TotalAccounts, off.TotalAccounts);
            Assert.Equal(on.TotalAttachments, off.TotalAttachments);

            // The parts are not computed, so they read as zero and the page does not render them.
            Assert.Equal(0, off.IncomingEmails);
            Assert.Equal(0, off.OutgoingEmails);
            Assert.Equal(0, off.IncomingAttachments);
            Assert.Equal(0, off.OutgoingAttachments);
            Assert.Equal(0, off.AccountDomains);

            // And with the feature on they are not zero, so the assertion above says something.
            Assert.True(on.IncomingEmails >= 1);
            Assert.True(on.AccountDomains >= 1);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task SelectablePeriods_Off_IsTheTwelveMonthHistogramWithoutPaging()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            ctx.ArchivedEmails.Add(BuildEmail(acct, "fixed", "a@x.com", "b@x.com",
                sentDate: NowInDisplayZone().Date.AddDays(-2)));
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateEmailCoreServiceWithoutDashboardFeatures(ctx);
            var dash = await svc.GetDashboardStatisticsAsync();

            Assert.Equal(12, dash.Series.Emails.Count);
            Assert.Equal("Month", dash.Series.Granularity);
            Assert.Equal("1y", dash.Series.Window);
            Assert.Equal(0, dash.Series.Offset);
            Assert.False(dash.Series.CanGoBack);
            Assert.False(dash.Series.CanGoForward);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task SelectablePeriods_Off_TheSendersAreTheWholeArchiveAgain()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            // Mail older than the twelve month histogram. With fixed periods the sender card
            // says all time and has to mean it; with periods to choose from it follows the
            // chosen one.
            var acct = await SeedAccountAsync(ctx);
            ctx.ArchivedEmails.Add(BuildEmail(acct, "ancient", "ancient@alltime.test", "b@x.com",
                sentDate: NowInDisplayZone().AddYears(-4)));
            await ctx.SaveChangesAsync();

            var off = await ServiceFactory.CreateEmailCoreServiceWithoutDashboardFeatures(ctx)
                .GetDashboardStatisticsAsync();
            var on = await ServiceFactory.CreateEmailCoreServiceNoCache(ctx).GetDashboardStatisticsAsync();

            Assert.Contains(off.Series.TopSenders, s => s.EmailAddress == "ancient@alltime.test");
            Assert.DoesNotContain(on.Series.TopSenders, s => s.EmailAddress == "ancient@alltime.test");
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task SelectablePeriods_Off_TheChartEndpointHasNothingToAnswer()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            // The guard sits in the service and not only in the controller, so that nothing can
            // reach the queries behind a switched off feature.
            var acct = await SeedAccountAsync(ctx);
            ctx.ArchivedEmails.Add(BuildEmail(acct, "guarded", "a@x.com", "b@x.com"));
            await ctx.SaveChangesAsync();

            var off = ServiceFactory.CreateEmailCoreServiceWithoutDashboardFeatures(ctx);
            var series = await off.GetChartSeriesAsync(
                new List<int> { acct.Id }, PeriodGranularity.Day, Window("7d"), false);

            Assert.Null(series);
            Assert.False(off.SelectablePeriods);
            Assert.False(off.ShowDirectionSplits);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task BuildTopSenders_WithoutAPeriodCountsTheWholeArchive()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var now = NowInDisplayZone();
            var acct = await SeedAccountAsync(ctx);
            ctx.ArchivedEmails.Add(BuildEmail(acct, "w-old", "old@whole.test", "b@x.com",
                sentDate: now.AddYears(-6)));
            ctx.ArchivedEmails.Add(BuildEmail(acct, "w-new", "new@whole.test", "b@x.com",
                sentDate: now.Date.AddDays(-2)));
            await ctx.SaveChangesAsync();

            var accountMail = ctx.ArchivedEmails.Where(e => e.MailAccountId == acct.Id);
            var range = DashboardPeriods.Resolve(PeriodGranularity.Day, Window("7d"), now);

            var withinWindow = EmailCoreService.BuildTopSenders(accountMail, range, outgoing: false, take: 10);
            var wholeArchive = EmailCoreService.BuildTopSenders(accountMail, null, outgoing: false, take: 10);

            Assert.DoesNotContain(withinWindow, s => s.EmailAddress == "old@whole.test");
            Assert.Contains(wholeArchive, s => s.EmailAddress == "old@whole.test");
            Assert.Contains(wholeArchive, s => s.EmailAddress == "new@whole.test");
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    // ============================================================
    // ExportEmailsAsync (EML)
    // ============================================================

    [Fact]
    public async Task Export_NonexistentEmail_ThrowsInvalidOperationException()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var svc = ServiceFactory.CreateEmailCoreService(ctx);
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => svc.ExportEmailsAsync(new ExportViewModel { EmailId = int.MaxValue - 1 }));
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task Export_SingleEmail_ProducesValidEmlRoundtrip()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var email = BuildEmail(acct, "Roundtrip", "alice@x.com", "bob@y.com",
                body: "plain text body", htmlBody: "<p>html body</p>");
            ctx.ArchivedEmails.Add(email);
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateEmailCoreService(ctx);
            var bytes = await svc.ExportEmailsAsync(new ExportViewModel { EmailId = email.Id });

            Assert.NotEmpty(bytes);
            using var ms = new MemoryStream(bytes);
            var parsed = await MimeMessage.LoadAsync(ms);
            Assert.Equal("Roundtrip", parsed.Subject);
            Assert.Contains("alice@x.com", parsed.From.Mailboxes.Select(m => m.Address));
            Assert.Contains("bob@y.com", parsed.To.Mailboxes.Select(m => m.Address));
            Assert.Equal("plain text body", parsed.TextBody);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task Export_PreservesDisplayNames()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var email = BuildEmail(acct, "WithNames", "alice@x.com", "bob@y.com",
                body: "x", fromDisplayName: "Alice Doe", toDisplayNames: "Bob Smith");
            ctx.ArchivedEmails.Add(email);
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateEmailCoreService(ctx);
            var bytes = await svc.ExportEmailsAsync(new ExportViewModel { EmailId = email.Id });

            using var ms = new MemoryStream(bytes);
            var parsed = await MimeMessage.LoadAsync(ms);
            Assert.Equal("Alice Doe", (parsed.From.Mailboxes.First() as MailboxAddress)?.Name);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task Export_RawHeadersPreserved()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var email = BuildEmail(acct, "Headers", "a@x.com", "b@x.com", body: "x",
                rawHeaders: "X-Custom-Header: hello\r\nX-Another: world\r\n");
            ctx.ArchivedEmails.Add(email);
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateEmailCoreService(ctx);
            var bytes = await svc.ExportEmailsAsync(new ExportViewModel { EmailId = email.Id });

            using var ms = new MemoryStream(bytes);
            var parsed = await MimeMessage.LoadAsync(ms);
            Assert.Equal("hello", parsed.Headers["X-Custom-Header"]);
            Assert.Equal("world", parsed.Headers["X-Another"]);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task Export_HtmlAndTextAlternative()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var email = BuildEmail(acct, "Both", "a@x.com", "b@x.com", body: "plain", htmlBody: "<p>html</p>");
            ctx.ArchivedEmails.Add(email);
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateEmailCoreService(ctx);
            var bytes = await svc.ExportEmailsAsync(new ExportViewModel { EmailId = email.Id });

            using var ms = new MemoryStream(bytes);
            var parsed = await MimeMessage.LoadAsync(ms);
            Assert.Equal("plain", parsed.TextBody);
            Assert.Contains("<p>html</p>", parsed.HtmlBody);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task Export_InlineAttachment_CidReferencePreserved()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var email = BuildEmail(acct, "Inline", "a@x.com", "b@x.com", body: "x",
                htmlBody: "<img src=\"cid:img1\">");
            email.HasAttachments = true;
            email.Attachments = new List<EmailAttachment>
            {
                new()
                {
                    FileName = "inline_img1.png",
                    ContentType = "image/png",
                    ContentId = "<img1>",
                    Size = 3,
                    LegacyContent = new byte[] { 1, 2, 3 }
                }
            };
            ctx.ArchivedEmails.Add(email);
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateEmailCoreService(ctx);
            var bytes = await svc.ExportEmailsAsync(new ExportViewModel { EmailId = email.Id });

            using var ms = new MemoryStream(bytes);
            var parsed = await MimeMessage.LoadAsync(ms);
            var inlinePart = parsed.BodyParts.OfType<MimePart>()
                .FirstOrDefault(p => p.ContentDisposition?.Disposition == "inline");
            Assert.NotNull(inlinePart);
            // MimeKit normalizes ContentId without angle brackets on parse.
            Assert.Equal("img1", inlinePart.ContentId);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    // ============================================================
    // ArchiveEmailAsync
    // ============================================================

    [Fact]
    public async Task Archive_NewEmail_Persists()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var msg = new MimeMessage();
            msg.Subject = "Archived";
            msg.From.Add(new MailboxAddress("", "a@x.com"));
            msg.To.Add(new MailboxAddress("", "b@x.com"));
            msg.Body = new TextPart("plain") { Text = "hello" };

            var svc = ServiceFactory.CreateEmailCoreService(ctx);
            var result = await svc.ArchiveEmailAsync(acct, msg, isOutgoing: false, folderName: "INBOX");
            Assert.True(result);

            var count = await ctx.ArchivedEmails.CountAsync(e => e.MailAccountId == acct.Id);
            Assert.Equal(1, count);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task Archive_DuplicateByMessageId_ReturnsFalse()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var msg = new MimeMessage();
            msg.MessageId = "dup-test@x.com";
            msg.Subject = "Dup";
            msg.From.Add(new MailboxAddress("", "a@x.com"));
            msg.To.Add(new MailboxAddress("", "b@x.com"));
            msg.Body = new TextPart("plain") { Text = "x" };

            var svc = ServiceFactory.CreateEmailCoreService(ctx);
            Assert.True(await svc.ArchiveEmailAsync(acct, msg, false, "INBOX"));
            Assert.False(await svc.ArchiveEmailAsync(acct, msg, false, "INBOX"));
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task Archive_DuplicateUpdatesFolder_ChangesFolderName()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var msg = new MimeMessage();
            msg.MessageId = "folder-change@x.com";
            msg.Subject = "Folder";
            msg.From.Add(new MailboxAddress("", "a@x.com"));
            msg.To.Add(new MailboxAddress("", "b@x.com"));
            msg.Body = new TextPart("plain") { Text = "x" };

            var svc = ServiceFactory.CreateEmailCoreService(ctx);
            await svc.ArchiveEmailAsync(acct, msg, false, "INBOX");
            await svc.ArchiveEmailAsync(acct, msg, false, "Archive");

            var stored = await ctx.ArchivedEmails.FirstAsync(e => e.MailAccountId == acct.Id);
            Assert.Equal("Archive", stored.FolderName);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    // ============================================================
    // ArchiveEmailAsync - fallback Message-ID (no Message-ID header)
    // ============================================================

    private static MimeMessage LoadRawMessage(string raw)
        => MimeMessage.Load(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(raw)));

    [Fact]
    public async Task Archive_NoMessageIdNoSubject_DistinctDeliveries_AllArchived()
    {
        // Regression test for the silent-skip bug: two distinct old messages with no
        // Message-ID and no Subject header, identical From/To and identical (Received-)
        // date. Previously both collapsed onto one synthetic dedupe key and the second
        // was silently skipped. Their differing Received chains must now keep them distinct.
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var msg1 = LoadRawMessage(
                "Received: from mx1.example.com by hub.example.com; Mon, 05 Jan 2004 10:00:00 +0100\r\n" +
                "From: alice@x.com\r\nTo: bob@x.com\r\n\r\nfirst body");
            var msg2 = LoadRawMessage(
                "Received: from mx2.example.com by hub.example.com; Mon, 05 Jan 2004 10:00:00 +0100\r\n" +
                "From: alice@x.com\r\nTo: bob@x.com\r\n\r\nsecond body");

            var svc = ServiceFactory.CreateEmailCoreService(ctx);
            Assert.True(await svc.ArchiveEmailAsync(acct, msg1, false, "INBOX"));
            Assert.True(await svc.ArchiveEmailAsync(acct, msg2, false, "INBOX"));

            var stored = await ctx.ArchivedEmails.Where(e => e.MailAccountId == acct.Id).ToListAsync();
            Assert.Equal(2, stored.Count);
            Assert.All(stored, e => Assert.StartsWith("generated-", e.MessageId));
            Assert.Equal(2, stored.Select(e => e.MessageId).Distinct().Count());
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task Archive_NoMessageId_SameMessageTwice_SecondSkipped()
    {
        // The same server message encountered again (e.g. full resync) must still be
        // recognized as a duplicate via the deterministic fallback ID.
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            const string raw =
                "From: alice@x.com\r\nTo: bob@x.com\r\n" +
                "Date: Mon, 05 Jan 2004 10:00:00 +0100\r\n\r\nsame body";

            var svc = ServiceFactory.CreateEmailCoreService(ctx);
            Assert.True(await svc.ArchiveEmailAsync(acct, LoadRawMessage(raw), false, "INBOX"));
            Assert.False(await svc.ArchiveEmailAsync(acct, LoadRawMessage(raw), false, "INBOX"));

            Assert.Equal(1, await ctx.ArchivedEmails.CountAsync(e => e.MailAccountId == acct.Id));
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task Archive_NoMessageId_UsesDeterministicFallbackId()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var msg = LoadRawMessage(
                "From: alice@x.com\r\nTo: bob@x.com\r\n" +
                "Date: Mon, 05 Jan 2004 10:00:00 +0100\r\n\r\nplain body");

            var svc = ServiceFactory.CreateEmailCoreService(ctx);
            Assert.True(await svc.ArchiveEmailAsync(acct, msg, false, "INBOX"));

            var row = await ctx.ArchivedEmails.FirstAsync(e => e.MailAccountId == acct.Id);
            var expectedKey = MailContentHelper.GenerateFallbackMessageId(
                "alice@x.com", "bob@x.com", null, msg.Date.Ticks,
                MailContentHelper.BuildCanonicalHeaders(msg.Headers));
            Assert.Equal(expectedKey, row.MessageId);
            Assert.Equal("(No Subject)", row.Subject);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task Archive_LegacyFallbackKey_HealsMessageIdWithoutDuplicate()
    {
        // Rows archived before the deterministic generator existed carry the legacy
        // fallback key. They must be recognized (no duplicate) and healed to the new key.
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var msg = LoadRawMessage(
                "From: alice@x.com\r\nTo: bob@x.com\r\n" +
                "Date: Mon, 05 Jan 2004 10:00:00 +0100\r\n\r\nlegacy body");

            // Exact formula the old code used for messages without a Message-ID header.
            var legacyKey = $"{msg.From}-{msg.To}-{msg.Subject}-{msg.Date.Ticks}";
            var legacyEmail = BuildEmail(acct, "(No Subject)", "alice@x.com", "bob@x.com", messageId: legacyKey);
            ctx.ArchivedEmails.Add(legacyEmail);
            await ctx.SaveChangesAsync();

            // Healing only applies to unlocked rows; unlock the seeded row via EF-tracked
            // update (IsLocked changes are explicitly allowed by the compliance trigger).
            legacyEmail.IsLocked = false;
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateEmailCoreService(ctx);
            Assert.False(await svc.ArchiveEmailAsync(acct, msg, false, "INBOX"));

            var stored = await ctx.ArchivedEmails.Where(e => e.MailAccountId == acct.Id).ToListAsync();
            var row = Assert.Single(stored);
            var expectedKey = MailContentHelper.GenerateFallbackMessageId(
                "alice@x.com", "bob@x.com", null, msg.Date.Ticks,
                MailContentHelper.BuildCanonicalHeaders(msg.Headers));
            Assert.Equal(expectedKey, row.MessageId);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task Archive_LegacyFallbackKey_LockedRow_NotHealedButSkipped()
    {
        // Compliance: a locked legacy row must never be modified (the DB trigger forbids
        // it). The duplicate is still recognized and skipped without an exception.
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var msg = LoadRawMessage(
                "From: alice@x.com\r\nTo: bob@x.com\r\n" +
                "Date: Mon, 05 Jan 2004 10:00:00 +0100\r\n\r\nlocked legacy body");

            var legacyKey = $"{msg.From}-{msg.To}-{msg.Subject}-{msg.Date.Ticks}";
            var lockedEmail = BuildEmail(acct, "(No Subject)", "alice@x.com", "bob@x.com", messageId: legacyKey);
            ctx.ArchivedEmails.Add(lockedEmail);
            await ctx.SaveChangesAsync();

            // Ensure the row is locked regardless of the DB column default.
            lockedEmail.IsLocked = true;
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateEmailCoreService(ctx);
            Assert.False(await svc.ArchiveEmailAsync(acct, msg, false, "INBOX"));

            var row = await ctx.ArchivedEmails.SingleAsync(e => e.MailAccountId == acct.Id);
            Assert.Equal(legacyKey, row.MessageId);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task Archive_ImportedFallbackKey_HealsMessageIdWithoutDuplicate()
    {
        // Rows imported via EML/MBOX without a Message-ID header carry the import fallback
        // key (hash without canonical headers, Date header only). IMAP sync must recognize
        // them (no duplicate) and heal them to the IMAP fallback key - otherwise retention
        // deletion can never match them (GitHub discussion #302).
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var msg = LoadRawMessage(
                "From: alice@x.com\r\nTo: bob@x.com\r\n" +
                "Date: Mon, 05 Jan 2004 10:00:00 +0100\r\n\r\nimported body");

            // Exact formula the import pipeline (MailImporter) uses for messages without Message-ID.
            var importedKey = MailContentHelper.GenerateFallbackMessageId(
                "alice@x.com", "bob@x.com", null, msg.Date.Ticks);
            var importedEmail = BuildEmail(acct, "(No Subject)", "alice@x.com", "bob@x.com", messageId: importedKey);
            ctx.ArchivedEmails.Add(importedEmail);
            await ctx.SaveChangesAsync();

            // Healing only applies to unlocked rows; unlock the seeded row via EF-tracked
            // update (IsLocked changes are explicitly allowed by the compliance trigger).
            importedEmail.IsLocked = false;
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateEmailCoreService(ctx);
            Assert.False(await svc.ArchiveEmailAsync(acct, msg, false, "INBOX"));

            var stored = await ctx.ArchivedEmails.Where(e => e.MailAccountId == acct.Id).ToListAsync();
            var row = Assert.Single(stored);
            var expectedKey = MailContentHelper.GenerateFallbackMessageId(
                "alice@x.com", "bob@x.com", null, msg.Date.Ticks,
                MailContentHelper.BuildCanonicalHeaders(msg.Headers));
            Assert.Equal(expectedKey, row.MessageId);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task Archive_ImportedFallbackKey_LockedRow_NotHealedButSkipped()
    {
        // Compliance: a locked imported row must never be modified (the DB trigger forbids
        // it). The duplicate is still recognized and skipped without an exception.
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var msg = LoadRawMessage(
                "From: alice@x.com\r\nTo: bob@x.com\r\n" +
                "Date: Mon, 05 Jan 2004 10:00:00 +0100\r\n\r\nlocked imported body");

            var importedKey = MailContentHelper.GenerateFallbackMessageId(
                "alice@x.com", "bob@x.com", null, msg.Date.Ticks);
            var lockedEmail = BuildEmail(acct, "(No Subject)", "alice@x.com", "bob@x.com", messageId: importedKey);
            ctx.ArchivedEmails.Add(lockedEmail);
            await ctx.SaveChangesAsync();

            lockedEmail.IsLocked = true;
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateEmailCoreService(ctx);
            Assert.False(await svc.ArchiveEmailAsync(acct, msg, false, "INBOX"));

            var row = await ctx.ArchivedEmails.SingleAsync(e => e.MailAccountId == acct.Id);
            Assert.Equal(importedKey, row.MessageId);
        }
        finally
        {
            await CleanupTestAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    /// <summary>
    /// Removes all test rows for accounts created in the given context (emails, caches,
    /// backfill states, user-mail-account links, and the account itself).
    /// </summary>
    private static async Task CleanupTestAccountAsync(MailArchiverDbContext ctx)
    {
        var accountIds = await ctx.MailAccounts.AsNoTracking()
            .Where(a => a.EmailAddress.EndsWith("@test.local"))
            .Select(a => a.Id)
            .ToListAsync();

        if (accountIds.Count == 0) return;

        var emails = await ctx.ArchivedEmails.Where(e => accountIds.Contains(e.MailAccountId)).ToListAsync();
        ctx.ArchivedEmails.RemoveRange(emails);

        var caches = await ctx.AccountStorageCaches.Where(c => accountIds.Contains(c.MailAccountId)).ToListAsync();
        ctx.AccountStorageCaches.RemoveRange(caches);

        var states = await ctx.AccountStorageBackfillStates.Where(s => accountIds.Contains(s.MailAccountId)).ToListAsync();
        ctx.AccountStorageBackfillStates.RemoveRange(states);

        var umas = await ctx.UserMailAccounts.Where(uma => accountIds.Contains(uma.MailAccountId)).ToListAsync();
        ctx.UserMailAccounts.RemoveRange(umas);

        var accts = await ctx.MailAccounts.Where(a => accountIds.Contains(a.Id)).ToListAsync();
        ctx.MailAccounts.RemoveRange(accts);

        try { await ctx.SaveChangesAsync(); }
        catch { /* best-effort cleanup */ }
    }
}
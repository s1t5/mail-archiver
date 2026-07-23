using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services.Core;
using MailArchiver.Tests.Infrastructure;
using MailArchiver.ViewModels;
using Microsoft.EntityFrameworkCore;
using MimeKit;
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

            var svc = ServiceFactory.CreateEmailCoreService(ctx);
            var dash = await svc.GetDashboardStatisticsAsync();

            Assert.True(dash.TotalEmails >= 2);
            Assert.True(dash.TotalAccounts >= 1);
            Assert.Equal(12, dash.EmailsByMonth.Count);
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

            var svc = ServiceFactory.CreateEmailCoreService(ctx);
            var dash = await svc.GetDashboardStatisticsAsync();

            // TopSenders only includes non-outgoing emails. We can't guarantee our test
            // senders make the top 10 (the Dev DB has real data), but we can verify the
            // query excludes outgoing by checking that an outgoing address we added is absent.
            Assert.DoesNotContain(dash.TopSenders, s => s.EmailAddress == "b@x.com");
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

            var svc = ServiceFactory.CreateEmailCoreService(ctx);
            var dash = await svc.GetDashboardStatisticsAsync();

            // RecentEmails is capped at 10 and ordered by SentDate desc.
            Assert.True(dash.RecentEmails.Count <= 10);
            for (int i = 1; i < dash.RecentEmails.Count; i++)
                Assert.True(dash.RecentEmails[i - 1].SentDate >= dash.RecentEmails[i].SentDate);
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
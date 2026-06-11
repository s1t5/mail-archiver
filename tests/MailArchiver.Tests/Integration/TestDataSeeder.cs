using System.Text;
using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MailArchiver.Tests.Integration;

/// <summary>
/// Plaintext API keys and entity IDs produced by <see cref="TestDataSeeder"/>.
/// Plaintext keys are only available here because they are unrecoverable after
/// creation.
/// </summary>
public sealed record SeededData
{
    public required int AdminUserId { get; init; }
    public required int LimitedUserId { get; init; }
    public required int InactiveUserId { get; init; }

    public required int AccountAId { get; init; }   // visible to the limited user
    public required int AccountBId { get; init; }   // NOT visible to the limited user

    public required string AdminKey { get; init; }
    public required string LimitedKey { get; init; }
    public required string RevokedKey { get; init; }
    public required string ExpiredKey { get; init; }
    public required string InactiveUserKey { get; init; }

    // Body-fallback-chain sample emails (account A).
    public required int OriginalBodyEmailId { get; init; }
    public required int UntruncatedBodyEmailId { get; init; }
    public required int PlainBodyEmailId { get; init; }

    // Attachment sample (account A).
    public required int AttachmentEmailId { get; init; }
    public required int AttachmentId { get; init; }

    // An email in account B, used for cross-account 404 checks.
    public required int CrossAccountEmailId { get; init; }

    // Secrets stored on account A; responses must never contain these.
    public const string AccountASecretPassword = "SUPER_SECRET_IMAP_PASSWORD";
    public const string AccountASecretClientSecret = "SUPER_SECRET_OAUTH_SECRET";
    public const string AccountAName = "Support Mailbox";
    public const string AccountBName = "Archive Mailbox";
}

/// <summary>
/// Seeds a deterministic data set once per test run (see PostgresContainerFixture):
/// three users (admin / limited / inactive), two mail accounts, a mailbox grant
/// for the limited user, a representative set of archived emails (folders,
/// directions, dates, body-fallback variants, an attachment), and five API keys
/// covering the auth states.
/// </summary>
public static class TestDataSeeder
{
    public static async Task<SeededData> SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();
        var apiKeys = scope.ServiceProvider.GetRequiredService<IApiKeyService>();

        // --- Users ---
        var admin = new User { Username = "admin-int", Email = "admin-int@example.com", IsAdmin = true, IsActive = true };
        var limited = new User { Username = "limited-int", Email = "limited-int@example.com", IsAdmin = false, IsActive = true };
        var inactive = new User { Username = "inactive-int", Email = "inactive-int@example.com", IsAdmin = false, IsActive = false };
        db.Users.AddRange(admin, limited, inactive);
        await db.SaveChangesAsync();

        // --- Mail accounts (account A carries secrets to assert non-leakage) ---
        var accountA = new MailAccount
        {
            Name = SeededData.AccountAName,
            EmailAddress = "support@example.com",
            Provider = ProviderType.IMAP,
            IsEnabled = true,
            LastSync = new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc),
            ImapServer = "imap.example.com",
            Username = "imap-user",
            Password = SeededData.AccountASecretPassword,
            ClientId = "client-id-123",
            ClientSecret = SeededData.AccountASecretClientSecret
        };
        var accountB = new MailAccount
        {
            Name = SeededData.AccountBName,
            EmailAddress = "archive@example.com",
            Provider = ProviderType.IMAP,
            IsEnabled = true,
            LastSync = new DateTime(2026, 6, 2, 8, 0, 0, DateTimeKind.Utc),
            Password = "OTHER_SECRET"
        };
        db.MailAccounts.AddRange(accountA, accountB);
        await db.SaveChangesAsync();

        // Limited user may only see account A.
        db.UserMailAccounts.Add(new UserMailAccount { UserId = limited.Id, MailAccountId = accountA.Id });
        await db.SaveChangesAsync();

        // --- Emails in account A ---
        // 6 incoming invoices in INBOX, dated 2026-03-01..06.
        for (int i = 1; i <= 6; i++)
        {
            db.ArchivedEmails.Add(NewEmail(accountA.Id,
                subject: $"Invoice {1000 + i}",
                from: "billing@vendor.com",
                to: "support@example.com",
                folder: "INBOX",
                outgoing: false,
                sent: new DateTime(2026, 3, i, 9, 0, 0, DateTimeKind.Utc),
                body: $"Please find invoice {1000 + i} attached for your records."));
        }

        // 2 incoming in INBOX/Work.
        db.ArchivedEmails.Add(NewEmail(accountA.Id, "Project Alpha report", "pm@work.com", "support@example.com",
            "INBOX/Work", false, new DateTime(2026, 2, 10, 9, 0, 0, DateTimeKind.Utc), "Quarterly project alpha report."));
        db.ArchivedEmails.Add(NewEmail(accountA.Id, "Project Beta update", "pm@work.com", "support@example.com",
            "INBOX/Work", false, new DateTime(2026, 2, 11, 9, 0, 0, DateTimeKind.Utc), "Project beta status update."));

        // 2 outgoing in Sent.
        db.ArchivedEmails.Add(NewEmail(accountA.Id, "Re: Quotation", "support@example.com", "client@customer.com",
            "Sent", true, new DateTime(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc), "Here is the quotation you requested."));
        db.ArchivedEmails.Add(NewEmail(accountA.Id, "Order confirmation", "support@example.com", "client@customer.com",
            "Sent", true, new DateTime(2026, 1, 20, 9, 0, 0, DateTimeKind.Utc), "Your order has been confirmed."));

        // Body fallback chain variants (INBOX).
        var originalBodyEmail = NewEmail(accountA.Id, "Body chain original", "chain@example.com", "support@example.com",
            "INBOX", false, new DateTime(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc), "fallback plain body");
        originalBodyEmail.HtmlBody = "<p>fallback html body</p>";
        originalBodyEmail.BodyUntruncatedHtml = "<p>untruncated html should be skipped</p>";
        originalBodyEmail.BodyUntruncatedText = "untruncated text should be skipped";
        originalBodyEmail.OriginalBodyHtml = Encoding.UTF8.GetBytes("<p>ORIGINAL HTML BODY</p>");
        originalBodyEmail.OriginalBodyText = Encoding.UTF8.GetBytes("ORIGINAL TEXT BODY");

        var untruncatedBodyEmail = NewEmail(accountA.Id, "Body chain untruncated", "chain@example.com", "support@example.com",
            "INBOX", false, new DateTime(2026, 3, 11, 9, 0, 0, DateTimeKind.Utc), "regular plain body should be skipped");
        untruncatedBodyEmail.HtmlBody = "<p>regular html should be skipped</p>";
        untruncatedBodyEmail.BodyUntruncatedHtml = "<p>UNTRUNCATED HTML BODY</p>";
        untruncatedBodyEmail.BodyUntruncatedText = "UNTRUNCATED TEXT BODY";

        var plainBodyEmail = NewEmail(accountA.Id, "Body chain plain", "chain@example.com", "support@example.com",
            "INBOX", false, new DateTime(2026, 3, 12, 9, 0, 0, DateTimeKind.Utc), "PLAIN TEXT BODY");
        plainBodyEmail.HtmlBody = "<p>PLAIN HTML BODY</p>";

        db.ArchivedEmails.AddRange(originalBodyEmail, untruncatedBodyEmail, plainBodyEmail);

        // Email with an attachment (dedup-aware content).
        var attachmentEmail = NewEmail(accountA.Id, "Document delivery", "docs@example.com", "support@example.com",
            "INBOX", false, new DateTime(2026, 3, 13, 9, 0, 0, DateTimeKind.Utc), "See attached document.");
        attachmentEmail.HasAttachments = true;
        var content = new AttachmentContent
        {
            Hash = "0000000000000000000000000000000000000000000000000000000000000001",
            Content = Encoding.UTF8.GetBytes("%PDF-1.4 fake attachment bytes"),
            Size = Encoding.UTF8.GetByteCount("%PDF-1.4 fake attachment bytes")
        };
        var attachment = new EmailAttachment
        {
            FileName = "report.pdf",
            ContentType = "application/pdf",
            Size = content.Size,
            AttachmentContent = content
        };
        attachmentEmail.Attachments.Add(attachment);
        db.ArchivedEmails.Add(attachmentEmail);

        // --- Emails in account B (not visible to the limited user) ---
        var crossAccountEmail = NewEmail(accountB.Id, "Confidential B1", "secret@b.com", "archive@example.com",
            "INBOX", false, new DateTime(2026, 3, 5, 9, 0, 0, DateTimeKind.Utc), "Confidential content in account B.");
        db.ArchivedEmails.Add(crossAccountEmail);
        db.ArchivedEmails.Add(NewEmail(accountB.Id, "Confidential B2", "secret@b.com", "archive@example.com",
            "INBOX", false, new DateTime(2026, 3, 6, 9, 0, 0, DateTimeKind.Utc), "More confidential content in account B."));

        await db.SaveChangesAsync();

        // --- API keys ---
        var (_, adminKey) = await apiKeys.CreateAsync(admin.Id, "admin key", null);
        var (_, limitedKey) = await apiKeys.CreateAsync(limited.Id, "limited key", null);
        var (revokedEntity, revokedKey) = await apiKeys.CreateAsync(limited.Id, "revoked key", null);
        var (_, expiredKey) = await apiKeys.CreateAsync(limited.Id, "expired key", new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var (_, inactiveUserKey) = await apiKeys.CreateAsync(inactive.Id, "inactive user key", null);
        await apiKeys.RevokeAsync(revokedEntity.Id, limited.Id, isAdmin: false);

        return new SeededData
        {
            AdminUserId = admin.Id,
            LimitedUserId = limited.Id,
            InactiveUserId = inactive.Id,
            AccountAId = accountA.Id,
            AccountBId = accountB.Id,
            AdminKey = adminKey,
            LimitedKey = limitedKey,
            RevokedKey = revokedKey,
            ExpiredKey = expiredKey,
            InactiveUserKey = inactiveUserKey,
            OriginalBodyEmailId = originalBodyEmail.Id,
            UntruncatedBodyEmailId = untruncatedBodyEmail.Id,
            PlainBodyEmailId = plainBodyEmail.Id,
            AttachmentEmailId = attachmentEmail.Id,
            AttachmentId = attachment.Id,
            CrossAccountEmailId = crossAccountEmail.Id
        };
    }

    private static ArchivedEmail NewEmail(int accountId, string subject, string from, string to,
        string folder, bool outgoing, DateTime sent, string body) => new()
    {
        MailAccountId = accountId,
        MessageId = $"<{Guid.NewGuid():N}@example.com>",
        Subject = subject,
        Body = body,
        HtmlBody = $"<p>{body}</p>",
        From = from,
        To = to,
        Cc = string.Empty,
        Bcc = string.Empty,
        SentDate = sent,
        ReceivedDate = sent,
        IsOutgoing = outgoing,
        HasAttachments = false,
        FolderName = folder
    };
}

using MailArchiver.Data;
using MailArchiver.Models;
using Microsoft.EntityFrameworkCore;

namespace MailArchiver.Tests.Infrastructure;

/// <summary>Temporary mail account per test; its rows are deleted afterwards.</summary>
internal static class TestMailAccounts
{
    public static async Task RunWithAccountAsync(
        TestDbFixture fixture, ProviderType provider, Func<MailArchiverDbContext, MailAccount, Task> test)
    {
        await using var ctx = fixture.CreateContext();
        var account = new MailAccount
        {
            Name = $"acct-{Guid.NewGuid():N}"[..25],
            EmailAddress = $"{Guid.NewGuid():N}@test.local",
            Provider = provider,
            IsEnabled = true,
            LastSync = DateTime.UtcNow
        };
        ctx.MailAccounts.Add(account);
        await ctx.SaveChangesAsync();

        try
        {
            await test(ctx, account);
        }
        finally
        {
            ctx.ChangeTracker.Clear();
            await ctx.ArchivedEmails.Where(e => e.MailAccountId == account.Id).ExecuteDeleteAsync();
            await ctx.MailAccounts.Where(a => a.Id == account.Id).ExecuteDeleteAsync();
        }
    }

    /// <summary>Adds an archived row.</summary>
    public static async Task<ArchivedEmail> SeedRowAsync(
        MailArchiverDbContext ctx, MailAccount account, string messageId, string subject, DateTime sentDate,
        string folder = "INBOX")
    {
        var row = new ArchivedEmail
        {
            MailAccountId = account.Id,
            MessageId = messageId,
            Subject = subject,
            From = "sender@outlook.example",
            To = "me@test.local",
            Cc = string.Empty,
            Bcc = string.Empty,
            Body = "body",
            HtmlBody = string.Empty,
            SentDate = sentDate,
            ReceivedDate = DateTime.UtcNow,
            FolderName = folder
        };
        ctx.ArchivedEmails.Add(row);
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();
        return row;
    }
}

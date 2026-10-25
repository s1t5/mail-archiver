using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services.Shared;
using MailArchiver.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using System.Text;
using Xunit;

namespace MailArchiver.Tests.Services;

/// <summary>
/// Storage of the original message next to the archived email (RawMessages:Enabled).
/// </summary>
[Collection(TestDbFixture.CollectionName)]
public class RawMessageStorageTests
{
    private readonly TestDbFixture _fixture;
    public RawMessageStorageTests(TestDbFixture fixture) => _fixture = fixture;

    private static async Task<MailAccount> SeedAccountAsync(MailArchiverDbContext ctx)
    {
        var account = new MailAccount
        {
            Name = $"raw-{Guid.NewGuid():N}".Substring(0, 25),
            EmailAddress = $"{Guid.NewGuid():N}@rawtest.local",
            Provider = ProviderType.IMAP,
            IsEnabled = true,
            LastSync = DateTime.UtcNow
        };
        ctx.MailAccounts.Add(account);
        await ctx.SaveChangesAsync();
        return account;
    }

    private static (MimeMessage Message, byte[] Bytes) BuildMessage()
    {
        var text =
            "Received: from mx.example.org by mail.example.com; Sat, 24 Oct 2026 10:00:00 +0200\r\n" +
            "From: a@example.org\r\n" +
            "To: b@example.com\r\n" +
            "Subject: Raw storage\r\n" +
            "Date: Sat, 24 Oct 2026 10:00:00 +0200\r\n" +
            $"Message-ID: <{Guid.NewGuid():N}@example.org>\r\n" +
            "Content-Type: text/plain; charset=utf-8\r\n" +
            "\r\n" +
            "hello\r\n";
        var bytes = Encoding.UTF8.GetBytes(text);
        return (MimeMessage.Load(new MemoryStream(bytes)), bytes);
    }

    private static async Task CleanupAsync(MailArchiverDbContext ctx)
    {
        var accountIds = await ctx.MailAccounts
            .Where(a => a.EmailAddress.EndsWith("@rawtest.local"))
            .Select(a => a.Id)
            .ToListAsync();
        await ctx.ArchivedEmails.Where(e => accountIds.Contains(e.MailAccountId)).ExecuteDeleteAsync();
        await ctx.MailAccounts.Where(a => accountIds.Contains(a.Id)).ExecuteDeleteAsync();
    }

    [Fact]
    public async Task Archive_Enabled_StoresOriginalBytes()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var (msg, bytes) = BuildMessage();

            var svc = ServiceFactory.CreateEmailCoreServiceWithRawMessages(ctx);
            Assert.True(await svc.ArchiveEmailAsync(acct, msg, false, "INBOX",
                new RawMessageCapture(bytes, RawMessageSource.Imap)));

            var emailId = await ctx.ArchivedEmails.Where(e => e.MailAccountId == acct.Id).Select(e => e.Id).SingleAsync();
            var raw = await ctx.RawMessages.AsNoTracking().SingleAsync(r => r.ArchivedEmailId == emailId);
            Assert.Equal(RawMessageSource.Imap, raw.Source);
            Assert.Equal(bytes, RawMessageCodec.Decompress(raw));
        }
        finally
        {
            await CleanupAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task Archive_Disabled_StoresNoOriginal()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var (msg, bytes) = BuildMessage();

            var svc = ServiceFactory.CreateEmailCoreService(ctx);
            Assert.True(await svc.ArchiveEmailAsync(acct, msg, false, "INBOX",
                new RawMessageCapture(bytes, RawMessageSource.Imap)));

            var emailId = await ctx.ArchivedEmails.Where(e => e.MailAccountId == acct.Id).Select(e => e.Id).SingleAsync();
            Assert.False(await ctx.RawMessages.AnyAsync(r => r.ArchivedEmailId == emailId));
        }
        finally
        {
            await CleanupAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task DeletingEmail_RemovesOriginal()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var (msg, bytes) = BuildMessage();

            var svc = ServiceFactory.CreateEmailCoreServiceWithRawMessages(ctx);
            await svc.ArchiveEmailAsync(acct, msg, false, "INBOX", new RawMessageCapture(bytes, RawMessageSource.Imap));
            var emailId = await ctx.ArchivedEmails.Where(e => e.MailAccountId == acct.Id).Select(e => e.Id).SingleAsync();

            // Delete the way the deletion services do: the email row only, original not loaded
            ctx.ChangeTracker.Clear();
            ctx.ArchivedEmails.Remove(await ctx.ArchivedEmails.SingleAsync(e => e.Id == emailId));
            await ctx.SaveChangesAsync();

            Assert.False(await ctx.RawMessages.AnyAsync(r => r.ArchivedEmailId == emailId));
        }
        finally
        {
            await CleanupAsync(ctx);
            await ctx.DisposeAsync();
        }
    }
}

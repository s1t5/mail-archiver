using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services.Providers.Eml;
using MailArchiver.Services.Shared;
using MailArchiver.Tests.Infrastructure;
using MailArchiver.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MimeKit;
using Moq;

namespace MailArchiver.Tests.Services;

/// <summary>
/// #622: mails with a reused Message-ID are archived separately (IMAP sync, MBOX/EML import).
/// </summary>
[Collection(TestDbFixture.CollectionName)]
public class ReusedMessageIdImapImportTests
{
    private readonly TestDbFixture _fixture;
    public ReusedMessageIdImapImportTests(TestDbFixture fixture) => _fixture = fixture;

    private const string ReusedId = "reused-622@outlook.example";
    private static readonly DateTimeOffset FirstSent = new(2026, 3, 4, 9, 0, 0, TimeSpan.Zero);

    private static MimeMessage Mail(string subject, DateTimeOffset sent)
    {
        var m = new MimeMessage { MessageId = ReusedId, Subject = subject, Date = sent };
        m.From.Add(new MailboxAddress("", "sender@outlook.example"));
        m.To.Add(new MailboxAddress("", "me@test.local"));
        m.Body = new TextPart("plain") { Text = subject };
        return m;
    }

    private Task RunAsync(Func<MailArchiverDbContext, MailAccount, Task> test)
        => TestMailAccounts.RunWithAccountAsync(_fixture, ProviderType.IMAP, test);

    private Task<int> CountAsync(MailArchiverDbContext ctx, MailAccount account)
        => ctx.ArchivedEmails.AsNoTracking().CountAsync(e => e.MailAccountId == account.Id);

    // ------------------------------------------------------------------ IMAP

    [Fact]
    public Task Imap_ReusedIdWeeksLater_IsArchived() => RunAsync(async (ctx, account) =>
    {
        var svc = ServiceFactory.CreateEmailCoreService(ctx);

        Assert.True(await svc.ArchiveEmailAsync(account, Mail("Reminder", FirstSent), false, "INBOX"));
        Assert.True(await svc.ArchiveEmailAsync(account, Mail("Reminder", FirstSent.AddDays(21)), false, "INBOX"));

        Assert.Equal(2, await CountAsync(ctx, account));
    });

    [Fact]
    public Task Imap_ReusedIdSameDayDifferentSubject_IsArchived() => RunAsync(async (ctx, account) =>
    {
        var svc = ServiceFactory.CreateEmailCoreService(ctx);

        Assert.True(await svc.ArchiveEmailAsync(account, Mail("Reminder: A", FirstSent), false, "INBOX"));
        Assert.True(await svc.ArchiveEmailAsync(account, Mail("Reminder: B", FirstSent.AddHours(2)), false, "INBOX"));

        Assert.Equal(2, await CountAsync(ctx, account));
    });

    [Fact]
    public Task Imap_SameMailAgain_IsStillDuplicate() => RunAsync(async (ctx, account) =>
    {
        var svc = ServiceFactory.CreateEmailCoreService(ctx);

        Assert.True(await svc.ArchiveEmailAsync(account, Mail("Reminder", FirstSent), false, "INBOX"));
        Assert.False(await svc.ArchiveEmailAsync(account, Mail("Reminder", FirstSent), false, "INBOX"));

        Assert.Equal(1, await CountAsync(ctx, account));
    });

    [Fact]
    public Task Imap_ReusedIdInOtherFolder_DoesNotMoveTheFirstMail() => RunAsync(async (ctx, account) =>
    {
        var svc = ServiceFactory.CreateEmailCoreService(ctx);

        await svc.ArchiveEmailAsync(account, Mail("Reminder", FirstSent), false, "INBOX");
        await svc.ArchiveEmailAsync(account, Mail("Reminder", FirstSent.AddDays(21)), false, "Archive");

        var folders = await ctx.ArchivedEmails.AsNoTracking()
            .Where(e => e.MailAccountId == account.Id)
            .OrderBy(e => e.SentDate).Select(e => e.FolderName).ToListAsync();
        Assert.Equal(new[] { "INBOX", "Archive" }, folders);
    });

    // ------------------------------------------------------------------ MBOX / EML import

    private MailImporter CreateImporter()
    {
        var services = new ServiceCollection();
        services.AddDbContext<MailArchiverDbContext>(o => o.UseNpgsql(_fixture.ConnectionString));
        services.AddSingleton(new DateTimeHelper(
            Options.Create(new TimeZoneOptions { DisplayTimeZoneId = "Europe/Berlin" })));
        return new MailImporter(services.BuildServiceProvider(), NullLogger<MailImporter>.Instance,
            new EmlAttachmentCollector(NullLogger<EmlAttachmentCollector>.Instance),
            new Mock<IRawMessageStore>().Object);
    }

    [Fact]
    public Task Import_ReusedIdWeeksLater_IsImported() => RunAsync(async (ctx, account) =>
    {
        var importer = CreateImporter();

        Assert.True((await importer.ImportEmailToDatabase(Mail("Reminder", FirstSent), account, "job", "INBOX")).Success);
        var second = await importer.ImportEmailToDatabase(Mail("Reminder", FirstSent.AddDays(21)), account, "job", "INBOX");

        Assert.True(second.Success);
        Assert.Equal(2, await CountAsync(ctx, account));
    });

    [Fact]
    public Task Import_SameMailAgain_IsStillDuplicate() => RunAsync(async (ctx, account) =>
    {
        var importer = CreateImporter();

        Assert.True((await importer.ImportEmailToDatabase(Mail("Reminder", FirstSent), account, "job", "INBOX")).Success);
        var again = await importer.ImportEmailToDatabase(Mail("Reminder", FirstSent), account, "job", "INBOX");

        Assert.True(again.AlreadyExists);
        Assert.Equal(1, await CountAsync(ctx, account));
    });
}

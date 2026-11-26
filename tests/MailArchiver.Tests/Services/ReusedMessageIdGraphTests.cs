using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services.Providers.Graph;
using MailArchiver.Services.Shared;
using MailArchiver.Tests.Infrastructure;
using MailArchiver.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Graph.Models;
using Moq;

namespace MailArchiver.Tests.Services;

/// <summary>
/// #622: mails with a reused Message-ID are archived separately (M365).
/// </summary>
[Collection(TestDbFixture.CollectionName)]
public class ReusedMessageIdGraphTests
{
    private readonly TestDbFixture _fixture;
    public ReusedMessageIdGraphTests(TestDbFixture fixture) => _fixture = fixture;

    private const string ReusedId = "reused-622@outlook.example";
    private static readonly DateTimeOffset FirstSent = new(2026, 3, 4, 9, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeHelper Helper =
        new(Options.Create(new TimeZoneOptions { DisplayTimeZoneId = "Europe/Berlin" }));

    private static GraphMailArchiver CreateArchiver(MailArchiverDbContext ctx) =>
        new(ctx, NullLogger<GraphMailArchiver>.Instance, Helper, new Mock<IRawMessageStore>().Object);

    private static Task<ArchivedEmail> SeedRowAsync(MailArchiverDbContext ctx, MailAccount account,
        string subject, DateTimeOffset sent, string folder = "INBOX")
        => TestMailAccounts.SeedRowAsync(ctx, account, ReusedId, subject, Helper.ConvertToDisplayTimeZone(sent), folder);

    private static Message GraphMessage(string subject, DateTimeOffset? sent) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        InternetMessageId = "<" + ReusedId + ">",
        Subject = subject,
        SentDateTime = sent,
        From = new Recipient { EmailAddress = new EmailAddress { Address = "sender@outlook.example" } },
        ToRecipients = new List<Recipient> { new() { EmailAddress = new EmailAddress { Address = "me@test.local" } } }
    };

    private Task RunAsync(Func<MailArchiverDbContext, MailAccount, Task> test)
        => TestMailAccounts.RunWithAccountAsync(_fixture, ProviderType.M365, test);

    [Fact]
    public Task SameMessageAgain_IsDuplicate() => RunAsync(async (ctx, account) =>
    {
        await SeedRowAsync(ctx, account, "Reminder", FirstSent);

        var isDuplicate = await CreateArchiver(ctx).IsDuplicateAsync(
            account.Id, ReusedId, GraphMessage("Reminder", FirstSent), "INBOX");

        Assert.True(isDuplicate);
    });

    [Fact]
    public Task ReusedIdWeeksLater_IsNotDuplicate() => RunAsync(async (ctx, account) =>
    {
        await SeedRowAsync(ctx, account, "Reminder", FirstSent);

        var isDuplicate = await CreateArchiver(ctx).IsDuplicateAsync(
            account.Id, ReusedId, GraphMessage("Reminder", FirstSent.AddDays(21)), "INBOX");

        Assert.False(isDuplicate);
    });

    [Fact]
    public Task ReusedIdSameDayDifferentSubject_IsNotDuplicate() => RunAsync(async (ctx, account) =>
    {
        await SeedRowAsync(ctx, account, "Reminder: Project A", FirstSent);

        var isDuplicate = await CreateArchiver(ctx).IsDuplicateAsync(
            account.Id, ReusedId, GraphMessage("Reminder: Project B", FirstSent.AddHours(2)), "INBOX");

        Assert.False(isDuplicate);
    });

    [Fact]
    public Task StoredDateShiftedByHours_IsStillDuplicate() => RunAsync(async (ctx, account) =>
    {
        // Older rows can be off by a timezone offset.
        await SeedRowAsync(ctx, account, "Reminder", FirstSent.AddHours(-2));

        var isDuplicate = await CreateArchiver(ctx).IsDuplicateAsync(
            account.Id, ReusedId, GraphMessage("Reminder", FirstSent), "INBOX");

        Assert.True(isDuplicate);
    });

    [Fact]
    public Task MissingSentDateTime_MatchesOnSubject() => RunAsync(async (ctx, account) =>
    {
        // Without SentDateTime the row holds the archive time.
        await SeedRowAsync(ctx, account, "Reminder", DateTimeOffset.UtcNow.AddDays(-30));

        var isDuplicate = await CreateArchiver(ctx).IsDuplicateAsync(
            account.Id, ReusedId, GraphMessage("Reminder", null), "INBOX");

        Assert.True(isDuplicate);
    });

    [Fact]
    public Task ReusedIdInOtherFolder_DoesNotMoveTheFirstMail() => RunAsync(async (ctx, account) =>
    {
        var first = await SeedRowAsync(ctx, account, "Reminder", FirstSent, folder: "INBOX");

        await CreateArchiver(ctx).IsDuplicateAsync(
            account.Id, ReusedId, GraphMessage("Reminder", FirstSent.AddDays(21)), "Archive");

        var stored = await ctx.ArchivedEmails.AsNoTracking().SingleAsync(e => e.Id == first.Id);
        Assert.Equal("INBOX", stored.FolderName);
    });

    [Fact]
    public Task FolderMove_UpdatesTheMatchingRowOnly() => RunAsync(async (ctx, account) =>
    {
        var first = await SeedRowAsync(ctx, account, "Reminder", FirstSent, folder: "INBOX");
        var second = await SeedRowAsync(ctx, account, "Reminder", FirstSent.AddDays(21), folder: "INBOX");

        var isDuplicate = await CreateArchiver(ctx).IsDuplicateAsync(
            account.Id, ReusedId, GraphMessage("Reminder", FirstSent.AddDays(21)), "Archive");

        Assert.True(isDuplicate);
        var rows = await ctx.ArchivedEmails.AsNoTracking()
            .Where(e => e.MailAccountId == account.Id).ToDictionaryAsync(e => e.Id, e => e.FolderName);
        Assert.Equal("INBOX", rows[first.Id]);
        Assert.Equal("Archive", rows[second.Id]);
    });
}

using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services.Shared;
using MailArchiver.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using static MailArchiver.Tests.Infrastructure.TestMailAccounts;

namespace MailArchiver.Tests.Services;

/// <summary>
/// Message-ID match used by archiving and by deletion after archiving (#622).
/// </summary>
[Collection(TestDbFixture.CollectionName)]
public class MessageIdMatchTests
{
    private readonly TestDbFixture _fixture;
    public MessageIdMatchTests(TestDbFixture fixture) => _fixture = fixture;

    private const string ReusedId = "reused-622@outlook.example";
    private static readonly DateTime FirstSent = new(2026, 3, 4, 10, 0, 0);

    private Task RunAsync(Func<MailArchiverDbContext, MailAccount, Task> test)
        => RunWithAccountAsync(_fixture, ProviderType.IMAP, test);

    private static Task SeedAsync(MailArchiverDbContext ctx, MailAccount account, string subject, DateTime sent)
        => SeedRowAsync(ctx, account, ReusedId, subject, sent);

    private static Task<bool> IsArchivedAsync(MailArchiverDbContext ctx, MailAccount account,
        string subject, DateTime? sent, bool isHeaderMessageId = true)
    {
        var match = new MessageIdMatch(new[] { ReusedId }, isHeaderMessageId, subject, sent);
        return match.AnyConfirmedAsync(ctx.ArchivedEmails
            .Where(e => e.MailAccountId == account.Id)
            .Where(match.Candidates));
    }

    [Fact]
    public Task SameMail_IsArchived() => RunAsync(async (ctx, account) =>
    {
        await SeedAsync(ctx, account, "Reminder", FirstSent);
        Assert.True(await IsArchivedAsync(ctx, account, "Reminder", FirstSent));
    });

    [Fact]
    public Task ReusedIdWeeksLater_IsNotArchived() => RunAsync(async (ctx, account) =>
    {
        await SeedAsync(ctx, account, "Reminder", FirstSent);
        Assert.False(await IsArchivedAsync(ctx, account, "Reminder", FirstSent.AddDays(21)));
    });

    [Fact]
    public Task ReusedIdDifferentSubject_IsNotArchived() => RunAsync(async (ctx, account) =>
    {
        await SeedAsync(ctx, account, "Reminder: A", FirstSent);
        Assert.False(await IsArchivedAsync(ctx, account, "Reminder: B", FirstSent.AddHours(1)));
    });

    [Fact]
    public Task DateWithinADay_IsArchived() => RunAsync(async (ctx, account) =>
    {
        await SeedAsync(ctx, account, "Reminder", FirstSent);
        Assert.True(await IsArchivedAsync(ctx, account, "Reminder", FirstSent.AddHours(-13)));
        Assert.True(await IsArchivedAsync(ctx, account, "Reminder", FirstSent.AddHours(23)));
    });

    [Fact]
    public Task ExactlyOneDayApart_IsNotArchived() => RunAsync(async (ctx, account) =>
    {
        await SeedAsync(ctx, account, "Reminder", FirstSent);
        Assert.False(await IsArchivedAsync(ctx, account, "Reminder", FirstSent.AddDays(1)));
        Assert.False(await IsArchivedAsync(ctx, account, "Reminder", FirstSent.AddDays(-1)));
    });

    [Theory]
    [InlineData("RE:  Offer\tdetails ", "RE: Offer details")]   // IMAP ENVELOPE collapses whitespace
    [InlineData("(No Subject)", "")]                            // missing subject vs. Graph
    [InlineData("", "(No Subject)")]
    public Task SubjectDifferingOnlyInWhitespaceOrMissing_IsArchived(string stored, string incoming)
        => RunAsync(async (ctx, account) =>
        {
            await SeedAsync(ctx, account, stored, FirstSent);
            Assert.True(await IsArchivedAsync(ctx, account, incoming, FirstSent));
            Assert.True(await IsArchivedAsync(ctx, account, incoming, null));
        });

    [Fact]
    public Task UnknownIncomingDate_MatchesOnSubject() => RunAsync(async (ctx, account) =>
    {
        await SeedAsync(ctx, account, "Reminder", FirstSent);
        Assert.True(await IsArchivedAsync(ctx, account, "Reminder", null));
        Assert.False(await IsArchivedAsync(ctx, account, "Other", null));
    });

    [Fact]
    public Task UnknownStoredDate_MatchesOnSubject() => RunAsync(async (ctx, account) =>
    {
        // Invalid Date headers were stored as year 1.
        await SeedAsync(ctx, account, "Reminder", new DateTime(1, 1, 1));
        Assert.True(await IsArchivedAsync(ctx, account, "Reminder", FirstSent));
    });

    [Fact]
    public Task FallbackKey_MatchesOnIdAlone() => RunAsync(async (ctx, account) =>
    {
        // Fallback IDs already contain subject and date.
        await SeedAsync(ctx, account, "Reminder", FirstSent);
        Assert.True(await IsArchivedAsync(ctx, account, "Other", FirstSent.AddDays(21), isHeaderMessageId: false));
    });
}

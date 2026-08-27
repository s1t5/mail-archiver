using MailArchiver.Models;
using MailArchiver.Services.Providers.Imap;
using MailArchiver.Utilities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MimeKit;

namespace MailArchiver.Tests.Services;

/// <summary>
/// The Date: header of a restored message should render its wall-clock time with the
/// configured display timezone's offset rather than a UTC offset. Both describe the same
/// instant; only the rendered offset differs. Before the fix the message was stamped with
/// an explicit +00:00 offset, which is correct but does not match what the user sees.
/// </summary>
public class ImapMailRestorerDateHeaderTests
{
    private static ImapMailRestorer CreateRestorer(string timeZoneId) => new(
        // No context is needed: only CreateMimeMessageFromArchivedEmailAsync is exercised,
        // and it reads from the passed-in entity alone.
        context: null!,
        NullLogger<ImapMailRestorer>.Instance,
        connectionFactory: null!,
        new DateTimeHelper(Options.Create(new TimeZoneOptions { DisplayTimeZoneId = timeZoneId })),
        Options.Create(new BatchOperationOptions()),
        Options.Create(new OffloadOptions()));

    [Fact]
    public async Task CreateMimeMessage_EmitsDateHeaderWithDisplayTimeZoneOffset()
    {
        var restorer = CreateRestorer("Europe/Berlin");
        var sentAsWallClock = new DateTime(2026, 7, 12, 10, 0, 0, DateTimeKind.Unspecified);
        var email = new ArchivedEmail
        {
            Subject = "Offset test",
            From = "sender@example.com",
            To = "recipient@example.com",
            SentDate = sentAsWallClock,
            // The archiving timestamp is not the delivery time and is deliberately wrong.
            ReceivedDate = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };

        var message = await restorer.CreateMimeMessageFromArchivedEmailAsync(email, "target");

        var berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        var expectedOffset = berlin.GetUtcOffset(sentAsWallClock);

        // The wall-clock time is preserved and rendered with the Berlin offset, not UTC.
        Assert.Equal(expectedOffset, message.Date.Offset);
        Assert.Equal(sentAsWallClock, message.Date.DateTime);
        // Same instant as the UTC conversion of the wall-clock value.
        Assert.Equal(
            TimeZoneInfo.ConvertTimeToUtc(sentAsWallClock, berlin),
            message.Date.UtcDateTime);
    }

    [Fact]
    public async Task CreateMimeMessage_InstantMatchesTheOldUtcBehavior()
    {
        // Guards the claim that the change is purely cosmetic: the instant does not move,
        // only the rendered offset. Berlin is +02:00 in July, so 10:00 Berlin == 08:00 UTC.
        var restorer = CreateRestorer("Europe/Berlin");
        var sentAsWallClock = new DateTime(2026, 7, 12, 10, 0, 0, DateTimeKind.Unspecified);
        var email = new ArchivedEmail
        {
            Subject = "Instant test",
            From = "sender@example.com",
            To = "recipient@example.com",
            SentDate = sentAsWallClock,
        };

        var message = await restorer.CreateMimeMessageFromArchivedEmailAsync(email, "target");

        Assert.Equal(new DateTimeOffset(2026, 7, 12, 8, 0, 0, TimeSpan.Zero),
            message.Date.ToUniversalTime());
    }
}

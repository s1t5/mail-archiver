using MailArchiver.Models;
using MailArchiver.Services.Shared;
using MailArchiver.Services.Providers.Imap;
using MailArchiver.Utilities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MimeKit;

namespace MailArchiver.Tests.Services;

/// <summary>
/// Archived address fields are stored values, not guaranteed RFC 5322 address lists. Exchange and
/// Microsoft 365 mailboxes store internal senders and recipients as X.500 distinguished names
/// ("/O=EXCHANGELABS/OU=.../CN=RECIPIENTS/CN=..."). Those used to abort the export of the whole
/// email: the parse failed, the fallback constructed a MailboxAddress from the same value (which
/// validates it again and throws again), and the exception dropped the message from the archive
/// output. Every affected email must survive the export, and no address may be invented.
/// </summary>
public class MailContentHelperStoredAddressTests
{
    private const string X500Dn =
        "/O=EXCHANGELABS/OU=EXCHANGE ADMINISTRATIVE GROUP (FYDIBOHF23SPDLT)/CN=RECIPIENTS/CN=ABC123-user";

    private static ImapMailRestorer CreateRestorer() => new(
        context: null!,
        NullLogger<ImapMailRestorer>.Instance,
        connectionFactory: null!,
        new DateTimeHelper(Options.Create(new TimeZoneOptions { DisplayTimeZoneId = "Europe/Berlin" })),
        Options.Create(new BatchOperationOptions()),
        Options.Create(new OffloadOptions()),
        folderService: null!);

    private static ArchivedEmail EmailWithAddresses(string? from, string? to = null, string? cc = null, string? bcc = null) => new()
    {
        Subject = "Address recovery",
        From = from ?? string.Empty,
        To = to ?? string.Empty,
        Cc = cc,
        Bcc = bcc,
        SentDate = new DateTime(2026, 10, 30, 9, 0, 0, DateTimeKind.Unspecified),
        ReceivedDate = new DateTime(2026, 10, 30, 9, 0, 1, DateTimeKind.Utc),
    };

    [Fact]
    public void AddStoredAddresses_KeepsX500DistinguishedNameInRecoveryHeader()
    {
        var message = new MimeMessage();

        MailContentHelper.AddStoredAddresses(message, message.From, "From", X500Dn, null);

        Assert.Empty(message.From);
        Assert.Equal(X500Dn, message.Headers["X-MailArchiver-Original-From"]);
    }

    [Fact]
    public void AddStoredAddresses_KeepsValidAddressesNextToAnUnparsableOne()
    {
        var message = new MimeMessage();

        MailContentHelper.AddStoredAddresses(
            message, message.To, "To", $"one@example.com, {X500Dn}, two@example.com", null);

        // The valid recipients are still there — a single bad entry must not discard the list.
        Assert.Equal(2, message.To.Count);
        Assert.Equal(new[] { "one@example.com", "two@example.com" }, message.To.Mailboxes.Select(m => m.Address));
        Assert.Equal(X500Dn, message.Headers["X-MailArchiver-Original-To"]);
    }

    [Fact]
    public void AddStoredAddresses_AppliesDisplayNamesWhenEverythingParsed()
    {
        var message = new MimeMessage();

        MailContentHelper.AddStoredAddresses(
            message, message.To, "To", "one@example.com, two@example.com", "One, Two");

        Assert.Equal(new[] { "One", "Two" }, message.To.Mailboxes.Select(m => m.Name));
        Assert.Null(message.Headers["X-MailArchiver-Original-To"]);
    }

    [Fact]
    public void AddStoredAddresses_DoesNotMisalignDisplayNamesWhenOneEntryIsUnparsable()
    {
        var message = new MimeMessage();

        // Two display names for two entries, but only one entry parses. Applying them positionally
        // would put the second name on the wrong address.
        MailContentHelper.AddStoredAddresses(
            message, message.To, "To", $"one@example.com, {X500Dn}", "One, Broken");

        var mailbox = Assert.Single(message.To.Mailboxes);
        Assert.Equal("one@example.com", mailbox.Address);
        Assert.True(string.IsNullOrEmpty(mailbox.Name), $"expected no display name, got '{mailbox.Name}'");
    }

    [Fact]
    public void AddStoredAddresses_DoesNotSplitCommasInsideQuotedDisplayName()
    {
        var message = new MimeMessage();

        MailContentHelper.AddStoredAddresses(
            message, message.To, "To", "\"Doe, Jane\" <jane@example.com>", null);

        var mailbox = Assert.Single(message.To.Mailboxes);
        Assert.Equal("Doe, Jane", mailbox.Name);
        Assert.Equal("jane@example.com", mailbox.Address);
        Assert.Null(message.Headers["X-MailArchiver-Original-To"]);
    }

    [Fact]
    public void AddStoredAddresses_DoesNothingForEmptyValue()
    {
        var message = new MimeMessage();

        MailContentHelper.AddStoredAddresses(message, message.From, "From", "   ", null);

        Assert.Empty(message.From);
        Assert.Null(message.Headers["X-MailArchiver-Original-From"]);
    }

    [Fact]
    public async Task RestoredMessage_WithX500Sender_IsBuiltAndKeepsOriginalValue()
    {
        var restorer = CreateRestorer();
        var email = EmailWithAddresses(from: X500Dn, to: "recipient@example.com");

        var message = await restorer.CreateMimeMessageFromArchivedEmailAsync(email, "target");

        Assert.NotNull(message);
        // No invented sender: the message must not claim sender@example.com.
        Assert.Empty(message.From);
        Assert.Equal(X500Dn, message.Headers["X-MailArchiver-Original-From"]);
        Assert.Equal("recipient@example.com", Assert.Single(message.To.Mailboxes).Address);
    }

    [Fact]
    public async Task RestoredMessage_WithX500Recipient_IsBuiltAndKeepsOriginalValue()
    {
        var restorer = CreateRestorer();
        var email = EmailWithAddresses(from: "sender@example.com", to: X500Dn, cc: X500Dn, bcc: X500Dn);

        var message = await restorer.CreateMimeMessageFromArchivedEmailAsync(email, "target");

        Assert.NotNull(message);
        Assert.Equal("sender@example.com", Assert.Single(message.From.Mailboxes).Address);
        Assert.Empty(message.To);
        Assert.Equal(X500Dn, message.Headers["X-MailArchiver-Original-To"]);
        Assert.Equal(X500Dn, message.Headers["X-MailArchiver-Original-Cc"]);
        Assert.Equal(X500Dn, message.Headers["X-MailArchiver-Original-Bcc"]);
    }

    [Fact]
    public async Task RestoredMessage_WithRecoveredHeader_CanBeWrittenAndParsedAgain()
    {
        var restorer = CreateRestorer();
        var email = EmailWithAddresses(from: X500Dn, to: "recipient@example.com");

        var message = await restorer.CreateMimeMessageFromArchivedEmailAsync(email, "target");

        // The export writes the message to a stream and it must still be loadable afterwards;
        // a header value that cannot be serialized would break the export just as the exception did.
        using var stream = new MemoryStream();
        await message.WriteToAsync(stream);
        stream.Position = 0;
        var reparsed = await MimeMessage.LoadAsync(stream);

        Assert.Equal(X500Dn, reparsed.Headers["X-MailArchiver-Original-From"]);
        Assert.Equal("recipient@example.com", Assert.Single(reparsed.To.Mailboxes).Address);
    }
}

using System.Text;
using MailArchiver.Services.Providers.Imap;

namespace MailArchiver.Tests.Services;

/// <summary>
/// The fetch half of the fallback needs a live IMAP server and is not unit tested, in line with
/// the rest of the IMAP I/O in this project. The parsing half is what decides whether a fallback
/// counts as a success, and that is pure: bytes in, message or null out. Returning null rather
/// than throwing is the contract the sync loop relies on to keep an unparseable response a
/// failure instead of an archived message.
/// </summary>
public class ImapMessageRecoveryTests
{
    private static MemoryStream Stream(string raw)
        => new(Encoding.UTF8.GetBytes(raw));

    [Fact]
    public async Task ParseAsync_valid_message_returns_it()
    {
        var raw = """
            From: Colleague <colleague@example.com>
            To: User <user@example.com>
            Subject: Recovered by the fallback

            Body text.
            """;

        var message = await ImapMessageRecovery.ParseAsync(Stream(raw));

        Assert.NotNull(message);
        Assert.Equal("Recovered by the fallback", message!.Subject);
    }

    [Fact]
    public async Task ParseAsync_exchange_placeholder_returns_it()
    {
        // The placeholder is a syntactically valid MIME document, so parsing it has to succeed.
        // Whether it is the original message is a separate question, answered by
        // ProviderPlaceholderDetector, not here.
        var raw = """
            MIME-Version: 1.0
            Content-Type: text/plain; charset="utf-8"
            From: Microsoft Exchange Server 2010 <exchange@example.com>
            To: User <user@example.com>
            Subject: Retrieval using the IMAP4 protocol failed for the following message: 270198

            The server couldn't retrieve the following message:
            Subject: "Example meeting"
            """;

        var message = await ImapMessageRecovery.ParseAsync(Stream(raw));

        Assert.NotNull(message);
        Assert.StartsWith("Retrieval using the IMAP4 protocol failed", message!.Subject);
    }

    [Fact]
    public async Task ParseAsync_empty_stream_returns_null()
    {
        Assert.Null(await ImapMessageRecovery.ParseAsync(new MemoryStream()));
    }
}

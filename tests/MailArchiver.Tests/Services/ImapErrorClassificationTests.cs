using System.IO;
using System.Net.Sockets;
using MailArchiver.Services.Providers.Imap;
using MailKit.Net.Imap;

namespace MailArchiver.Tests.Services;

public class ImapErrorClassificationTests
{
    /// <summary>
    /// Reproduces the Yahoo disconnect chain observed during FETCH:
    /// outer "IMAP4rev1 Server logging out" wrapping
    /// "The IMAP server has unexpectedly disconnected.".
    /// </summary>
    private static ImapProtocolException BuildYahooDisconnect() =>
        new ImapProtocolException("IMAP4rev1 Server logging out",
            new ImapProtocolException("The IMAP server has unexpectedly disconnected."));

    private static ImapProtocolException BuildParseError() =>
        new ImapProtocolException("Unexpected atom token: Server");

    [Fact]
    public void IsConnectionLoss_yahoo_disconnect_chain_returns_true()
    {
        Assert.True(ImapMailSyncService.IsConnectionLoss(BuildYahooDisconnect()));
    }

    [Fact]
    public void IsConnectionLoss_ioException_returns_true()
    {
        Assert.True(ImapMailSyncService.IsConnectionLoss(new IOException("Unable to read data from the connection")));
    }

    [Fact]
    public void IsConnectionLoss_socketException_returns_true()
    {
        Assert.True(ImapMailSyncService.IsConnectionLoss(new SocketException((int)SocketError.ConnectionReset)));
    }

    [Fact]
    public void IsConnectionLoss_socketException_nested_in_protocol_exception_returns_true()
    {
        var ex = new ImapProtocolException("The IMAP server has unexpectedly disconnected.",
            new SocketException((int)SocketError.ConnectionAborted));

        Assert.True(ImapMailSyncService.IsConnectionLoss(ex));
    }

    [Fact]
    public void IsConnectionLoss_parse_error_returns_false()
    {
        Assert.False(ImapMailSyncService.IsConnectionLoss(BuildParseError()));
    }

    [Fact]
    public void IsConnectionLoss_generic_exception_returns_false()
    {
        Assert.False(ImapMailSyncService.IsConnectionLoss(new InvalidOperationException("Some unrelated failure")));
    }

    [Fact]
    public void IsTransientImapError_yahoo_disconnect_chain_returns_true()
    {
        Assert.True(ImapMailSyncService.IsTransientImapError(BuildYahooDisconnect()));
    }

    [Fact]
    public void IsTransientImapError_throttling_no_response_returns_true()
    {
        var ex = new ImapCommandException(ImapCommandResponse.No,
            "NO [UNAVAILABLE] Service temporarily unavailable",
            "NO [UNAVAILABLE] Service temporarily unavailable");

        Assert.True(ImapMailSyncService.IsTransientImapError(ex));
    }

    [Fact]
    public void IsTransientImapError_generic_no_response_returns_false()
    {
        var ex = new ImapCommandException(ImapCommandResponse.No,
            "NO [NONEXISTENT] Unknown Mailbox",
            "NO [NONEXISTENT] Unknown Mailbox");

        Assert.False(ImapMailSyncService.IsTransientImapError(ex));
    }

    [Fact]
    public void IsTransientImapError_parse_error_returns_false()
    {
        Assert.False(ImapMailSyncService.IsTransientImapError(BuildParseError()));
    }

    [Fact]
    public void IsImapProtocolParseError_parse_error_returns_true()
    {
        Assert.True(ImapMailSyncService.IsImapProtocolParseError(BuildParseError()));
    }

    [Fact]
    public void IsImapProtocolParseError_parse_error_wrapped_returns_true()
    {
        var ex = new ImapCommandException(ImapCommandResponse.No,
            "Failed to fetch message",
            "NO fetch failed",
            BuildParseError());

        Assert.True(ImapMailSyncService.IsImapProtocolParseError(ex));
    }

    [Fact]
    public void IsImapProtocolParseError_yahoo_disconnect_chain_returns_false()
    {
        Assert.False(ImapMailSyncService.IsImapProtocolParseError(BuildYahooDisconnect()));
    }

    [Fact]
    public void IsImapProtocolParseError_ioException_returns_false()
    {
        Assert.False(ImapMailSyncService.IsImapProtocolParseError(new IOException("Connection reset by peer")));
    }

    [Fact]
    public void IsImapProtocolParseError_generic_exception_returns_false()
    {
        Assert.False(ImapMailSyncService.IsImapProtocolParseError(new InvalidOperationException("No connection available")));
    }
}

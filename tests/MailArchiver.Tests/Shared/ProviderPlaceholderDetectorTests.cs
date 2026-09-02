using System.Text;
using MailArchiver.Services.Shared;
using MimeKit;

namespace MailArchiver.Tests.Shared;

/// <summary>
/// The detector decides whether an archived message is real mail or the provider's apology for
/// mail it could not convert. A false positive would label a genuine message as a placeholder, so
/// every negative case here is a message that comes close on purpose: the right subject without
/// the body, the right body without the subject, and a Microsoft sender that means nothing by
/// itself.
///
/// The fixture is synthetic and modelled on a sanitized Exchange response. No production data.
/// </summary>
public class ProviderPlaceholderDetectorTests
{
    private static MimeMessage Parse(string raw)
        => MimeMessage.Load(new MemoryStream(Encoding.UTF8.GetBytes(raw)));

    private const string Placeholder = """
        MIME-Version: 1.0
        Content-Type: text/plain; charset="utf-8"
        From: Microsoft Exchange Server 2010 <exchange@example.com>
        To: User <user@example.com>
        Subject: Retrieval using the IMAP4 protocol failed for the following message: 270198

        The server couldn't retrieve the following message:
        Subject: "Example meeting"
        From: "Example Sender"
        Sent date: 20.12.2013 11:12:26

        The message hasn't been deleted.
        You might be able to view it using either Outlook or Outlook Web App.
        """;

    [Fact]
    public void Placeholder_subject_and_body_are_detected()
    {
        Assert.True(ProviderPlaceholderDetector.IsProviderRetrievalErrorPlaceholder(Parse(Placeholder)));
    }

    [Fact]
    public void Placeholder_with_typographic_apostrophe_is_detected()
    {
        var raw = Placeholder.Replace("couldn't", "couldn’t");
        Assert.True(ProviderPlaceholderDetector.IsProviderRetrievalErrorPlaceholder(Parse(raw)));
    }

    [Fact]
    public void Placeholder_with_spelled_out_wording_is_detected()
    {
        var raw = Placeholder.Replace(
            "The server couldn't retrieve the following message:",
            "The server could not retrieve the following message:");
        Assert.True(ProviderPlaceholderDetector.IsProviderRetrievalErrorPlaceholder(Parse(raw)));
    }

    [Fact]
    public void Placeholder_without_the_exchange_sender_is_still_detected()
    {
        var raw = Placeholder.Replace(
            "From: Microsoft Exchange Server 2010 <exchange@example.com>",
            "From: Mail Delivery System <postmaster@example.com>");
        Assert.True(ProviderPlaceholderDetector.IsProviderRetrievalErrorPlaceholder(Parse(raw)));
    }

    [Fact]
    public void Ordinary_mail_is_not_a_placeholder()
    {
        var raw = """
            From: Colleague <colleague@example.com>
            To: User <user@example.com>
            Subject: Quarterly review

            Please find the numbers attached.
            """;
        Assert.False(ProviderPlaceholderDetector.IsProviderRetrievalErrorPlaceholder(Parse(raw)));
    }

    [Fact]
    public void Microsoft_sender_alone_is_not_a_placeholder()
    {
        var raw = """
            From: Microsoft Exchange Server 2010 <exchange@example.com>
            To: User <user@example.com>
            Subject: Your mailbox is almost full

            Please delete some messages.
            """;
        Assert.False(ProviderPlaceholderDetector.IsProviderRetrievalErrorPlaceholder(Parse(raw)));
    }

    [Fact]
    public void Subject_marker_without_the_body_marker_is_not_a_placeholder()
    {
        var raw = """
            From: Colleague <colleague@example.com>
            To: User <user@example.com>
            Subject: Retrieval using the IMAP4 protocol failed for the following message: 270198

            Forwarding this because our archiver logged it. Any idea what it means?
            """;
        Assert.False(ProviderPlaceholderDetector.IsProviderRetrievalErrorPlaceholder(Parse(raw)));
    }

    [Fact]
    public void Body_marker_without_the_subject_marker_is_not_a_placeholder()
    {
        var raw = """
            From: Colleague <colleague@example.com>
            To: User <user@example.com>
            Subject: Re: that Exchange problem again

            The server couldn't retrieve the following message: that is the exact wording we see.
            """;
        Assert.False(ProviderPlaceholderDetector.IsProviderRetrievalErrorPlaceholder(Parse(raw)));
    }

    [Fact]
    public void Message_without_a_subject_is_not_a_placeholder()
    {
        var raw = """
            From: Colleague <colleague@example.com>
            To: User <user@example.com>

            The server couldn't retrieve the following message:
            """;
        Assert.False(ProviderPlaceholderDetector.IsProviderRetrievalErrorPlaceholder(Parse(raw)));
    }

    [Fact]
    public void Null_message_is_not_a_placeholder()
    {
        Assert.False(ProviderPlaceholderDetector.IsProviderRetrievalErrorPlaceholder(null));
    }

    [Fact]
    public void Original_subject_is_read_out_of_the_placeholder_body()
    {
        Assert.Equal("Example meeting",
            ProviderPlaceholderDetector.TryGetOriginalSubject(Parse(Placeholder)));
    }

    [Fact]
    public void Original_subject_is_null_when_the_body_does_not_quote_one()
    {
        var raw = Placeholder.Replace("Subject: \"Example meeting\"", "Subject: not quoted");
        Assert.Null(ProviderPlaceholderDetector.TryGetOriginalSubject(Parse(raw)));
    }

    [Fact]
    public void Original_subject_is_null_for_a_message_without_a_text_body()
    {
        Assert.Null(ProviderPlaceholderDetector.TryGetOriginalSubject(null));
    }
}

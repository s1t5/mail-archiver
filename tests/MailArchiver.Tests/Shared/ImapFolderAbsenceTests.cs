using MailArchiver.Services.Shared;
using MailKit;
using MailKit.Net.Imap;

namespace MailArchiver.Tests.Shared;

/// <summary>
/// A folder the server refuses to open is either gone or broken, and the two must not be confused.
///
/// Gone is an answer: RFC 3501 forbids a server to drop a name from the subscription list when the
/// mailbox behind it is deleted, so a folder somebody once subscribed to in an IMAP client is
/// reported by LSUB forever. Counting that as a failed folder holds LastSync back, and since the
/// folder never comes back the account stays held back for good — observed in the field on a mailbox
/// with 16 such names, stuck for two days.
///
/// Broken is not an answer: no permission, a dropped connection, throttling. Those can succeed next
/// time and must keep holding the account back until they do.
///
/// Every wrong "gone" silently drops a real folder from the archive, so the negative cases below
/// matter more than the positive ones.
/// </summary>
public class ImapFolderAbsenceTests
{
    // Three arguments on purpose: the two-argument overload does not put the text into Message,
    // and Message is what carries it in the wild — see the Exchange wording below, taken verbatim
    // from a production log.
    private static ImapCommandException No(string message)
        => new ImapCommandException(ImapCommandResponse.No, message, message);

    // ---- gone ---------------------------------------------------------------------------------

    [Fact]
    public void The_exchange_2010_wording_counts_as_gone()
    {
        // Verbatim from an Exchange 2010 IMAP4 EXAMINE on a stale subscription.
        Assert.True(ImapFolderAbsence.IsFolderGone(
            No("The IMAP server replied to the 'EXAMINE' command with a 'NO' response: INBOX/Kunden/BeLa doesn't exist.")));
    }

    [Theory]
    [InlineData("Mailbox does not exist")]
    [InlineData("No such mailbox")]
    [InlineData("NONEXISTENT MAILBOX")]
    public void Other_common_wordings_count_as_gone(string message)
    {
        Assert.True(ImapFolderAbsence.IsFolderGone(No(message)));
    }

    [Fact]
    public void The_rfc_5530_response_code_counts_as_gone()
    {
        // Servers new enough to send it say it without prose, and that is the reading we prefer.
        Assert.True(ImapFolderAbsence.IsFolderGone(No("[NONEXISTENT] Mailbox is not there any more")));
    }

    [Fact]
    public void A_folder_not_found_exception_counts_as_gone()
    {
        Assert.True(ImapFolderAbsence.IsFolderGone(new FolderNotFoundException("INBOX/Marketing")));
    }

    [Fact]
    public void An_absence_wrapped_in_another_exception_is_still_found()
    {
        var wrapped = new InvalidOperationException("while opening a folder",
            No("INBOX/Kunden/isbank doesn't exist."));

        Assert.True(ImapFolderAbsence.IsFolderGone(wrapped));
    }

    [Fact]
    public void Case_does_not_matter()
    {
        Assert.True(ImapFolderAbsence.IsFolderGone(No("Folder DOESN'T EXIST")));
    }

    // ---- not gone, and these are the ones that must not slip ----------------------------------

    [Fact]
    public void Nothing_at_all_is_not_an_absence()
    {
        Assert.False(ImapFolderAbsence.IsFolderGone(null));
    }

    [Theory]
    [InlineData("Permission denied")]
    [InlineData("Service temporarily unavailable")]
    [InlineData("Server busy, try again later")]
    [InlineData("[OVERQUOTA] Not enough disk space")]
    public void A_real_failure_stays_a_failure(string message)
    {
        Assert.False(ImapFolderAbsence.IsFolderGone(No(message)));
    }

    [Fact]
    public void A_dropped_connection_is_not_an_absence()
    {
        Assert.False(ImapFolderAbsence.IsFolderGone(new IOException("Connection reset by peer")));
    }

    [Fact]
    public void An_ok_response_that_mentions_the_wording_is_not_an_absence()
    {
        // Only a NO carries the meaning. Anything else quoting the phrase is not the server
        // refusing the mailbox.
        Assert.False(ImapFolderAbsence.IsFolderGone(
            new ImapCommandException(ImapCommandResponse.Ok,
                "the folder doesn't exist yet, creating it",
                "the folder doesn't exist yet, creating it")));
    }

    [Fact]
    public void A_plain_exception_carrying_the_wording_is_not_an_absence()
    {
        Assert.False(ImapFolderAbsence.IsFolderGone(new InvalidOperationException("the file doesn't exist")));
    }
}

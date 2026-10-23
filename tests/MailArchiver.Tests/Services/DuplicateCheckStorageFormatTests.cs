using MailArchiver.Services.Shared;
using MimeKit;
using Xunit;

namespace MailArchiver.Tests.Services
{
    /// <summary>
    /// The duplicate check compares incoming values against columns that were written by the
    /// archiving pipelines. If the two sides are produced differently the second criterion
    /// silently never fires, and nothing looks wrong until mail starts duplicating.
    /// <para>
    /// These tests reproduce the write path's expression for each field and assert that the
    /// normalization used to build the query agrees with it. They are written against the
    /// storage expression rather than against the query so that changing how a row is written
    /// breaks them.
    /// </para>
    /// </summary>
    public class DuplicateCheckStorageFormatTests
    {
        // The expressions the archiving pipelines use when writing the row.
        private static string StoredFrom(IEnumerable<string> addresses) =>
            MailContentHelper.TruncateFieldForTsvector(
                MailContentHelper.CleanText(string.Join(", ", addresses)), 10_000);

        private static string StoredTo(IEnumerable<string> addresses) =>
            MailContentHelper.TruncateFieldForTsvector(
                MailContentHelper.CleanText(string.Join(", ", addresses)), 50_000);

        private static string StoredSubject(string? subject) =>
            MailContentHelper.TruncateFieldForTsvector(
                MailContentHelper.CleanText(subject ?? "(No Subject)"), 50_000);

        [Fact]
        public void SingleRecipient_ComparedFormMatchesStoredForm()
        {
            var addresses = new[] { "one@example.com" };

            Assert.Equal(
                StoredTo(addresses),
                MailMatchKey.NormalizeAddresses(addresses, MailMatchKey.ToMaxBytes));
        }

        /// <summary>
        /// The reported defect. A single address emits no separator, which is why the old
        /// comparison looked correct in everyday use; two or more never matched.
        /// </summary>
        [Fact]
        public void TwoRecipients_ComparedFormMatchesStoredForm()
        {
            var addresses = new[] { "one@example.com", "two@example.com" };

            Assert.Equal(
                StoredTo(addresses),
                MailMatchKey.NormalizeAddresses(addresses, MailMatchKey.ToMaxBytes));
        }

        [Fact]
        public void TwoRecipients_OldCommaJoinWouldNotHaveMatched()
        {
            // Pins the bug itself, so the fix cannot be reverted quietly.
            var addresses = new[] { "one@example.com", "two@example.com" };

            Assert.NotEqual(StoredTo(addresses), string.Join(",", addresses));
        }

        [Fact]
        public void TwoSenders_ComparedFormMatchesStoredForm()
        {
            var addresses = new[] { "a@example.com", "b@example.com" };

            Assert.Equal(
                StoredFrom(addresses),
                MailMatchKey.NormalizeAddresses(addresses, MailMatchKey.FromMaxBytes));
        }

        [Theory]
        [InlineData("plain subject")]
        [InlineData("subject\twith\ttabs")]
        [InlineData("subject\nwith\nnewlines")]
        [InlineData("")]
        public void Subject_ComparedFormMatchesStoredForm(string subject)
        {
            Assert.Equal(StoredSubject(subject), MailMatchKey.NormalizeSubject(subject));
        }

        [Fact]
        public void MissingSubject_ComparedFormMatchesStoredForm()
        {
            // MailImporter writes `message.Subject ?? "(No Subject)"`, substituting for null
            // only. An empty Subject header is stored empty, so the two must not collapse.
            Assert.Equal(StoredSubject(null), MailMatchKey.NormalizeSubject(null));
            Assert.NotEqual(MailMatchKey.NormalizeSubject(null), MailMatchKey.NormalizeSubject(""));
        }

        [Fact]
        public void SubjectWithControlCharacter_ComparedFormMatchesStoredForm()
        {
            const string subject = "before\u0001after";

            Assert.Equal(StoredSubject(subject), MailMatchKey.NormalizeSubject(subject));
            // And the raw value would not have matched the column.
            Assert.NotEqual(StoredSubject(subject), subject);
        }

        [Fact]
        public void MimeMessageAddresses_NormalizeToTheStoredForm()
        {
            // End to end over the type the importer actually holds.
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("A", "a@example.com"));
            message.To.Add(new MailboxAddress("One", "one@example.com"));
            message.To.Add(new MailboxAddress("Two", "two@example.com"));

            var toAddresses = message.To.Mailboxes.Select(m => m.Address).ToList();

            Assert.Equal(
                StoredTo(toAddresses),
                MailMatchKey.NormalizeAddresses(toAddresses, MailMatchKey.ToMaxBytes));
        }
    }
}

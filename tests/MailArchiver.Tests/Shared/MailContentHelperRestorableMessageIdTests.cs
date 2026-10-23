using MailArchiver.Services.Shared;
using Xunit;

namespace MailArchiver.Tests.Shared
{
    /// <summary>
    /// <see cref="MailContentHelper.ToRestorableMessageId"/> and
    /// <see cref="MailContentHelper.MessageIdMatchCandidates"/>.
    /// </summary>
    public class MailContentHelperRestorableMessageIdTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("<>")]
        public void NoStoredId_YieldsEmpty(string? stored)
        {
            Assert.Equal(string.Empty, MailContentHelper.ToRestorableMessageId(stored));
        }

        [Theory]
        [InlineData("abc@host.example", "abc@host.example")]
        [InlineData("<abc@host.example>", "abc@host.example")]
        [InlineData("  <<abc@host.example>>  ", "abc@host.example")]
        public void UsableId_IsReturnedNormalized(string stored, string expected)
        {
            Assert.Equal(expected, MailContentHelper.ToRestorableMessageId(stored));
        }

        /// <summary>
        /// The case behind the defect: a stored value with no "@" is not a usable msg-id, so it
        /// used to be dropped and replaced by a fresh random one on every append.
        /// </summary>
        [Fact]
        public void UnusableId_IsReplacedByADerivedIdentifier()
        {
            var restorable = MailContentHelper.ToRestorableMessageId("not-a-message-id");

            Assert.NotEqual(string.Empty, restorable);
            Assert.Contains("@", restorable);
            Assert.DoesNotContain("not-a-message-id", restorable);
        }

        [Fact]
        public void UnusableId_IsStableAcrossCalls()
        {
            // The whole point: repeated appends of the same row must emit the same Message-ID,
            // otherwise the copies can never be recognized as duplicates of one another.
            var first = MailContentHelper.ToRestorableMessageId("legacy-value");
            var second = MailContentHelper.ToRestorableMessageId("legacy-value");

            Assert.Equal(first, second);
        }

        [Fact]
        public void UnusableIds_DifferFromEachOther()
        {
            Assert.NotEqual(
                MailContentHelper.ToRestorableMessageId("legacy-one"),
                MailContentHelper.ToRestorableMessageId("legacy-two"));
        }

        [Fact]
        public void UnusableId_IsIdempotentThroughASecondPass()
        {
            // A row re-archived from a restored copy must not drift to a new identifier.
            var once = MailContentHelper.ToRestorableMessageId("legacy-value");
            var twice = MailContentHelper.ToRestorableMessageId(once);

            Assert.Equal(once, twice);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void MatchCandidates_AreEmptyForAMissingId(string? stored)
        {
            Assert.Empty(MailContentHelper.MessageIdMatchCandidates(stored));
        }

        [Fact]
        public void MatchCandidates_CoverBareAndBracketedStorage()
        {
            var candidates = MailContentHelper.MessageIdMatchCandidates("<abc@host.example>");

            Assert.Contains("abc@host.example", candidates);
            Assert.Contains("<abc@host.example>", candidates);
        }

        [Fact]
        public void MatchCandidates_AreTheSameWhicheverFormIsPassedIn()
        {
            Assert.Equal(
                MailContentHelper.MessageIdMatchCandidates("abc@host.example"),
                MailContentHelper.MessageIdMatchCandidates("<abc@host.example>"));
        }
    }
}

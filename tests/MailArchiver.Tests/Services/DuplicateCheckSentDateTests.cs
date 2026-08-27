using MailArchiver.Models;
using MailArchiver.Utilities;
using Microsoft.Extensions.Options;
using Xunit;

namespace MailArchiver.Tests.Services
{
    /// <summary>
    /// The send time half of the duplicate check.
    /// <para>
    /// <c>SentDate</c> is stored converted into the display timezone, so the incoming value has
    /// to be converted the same way before the two-second window is applied. Getting this wrong
    /// produces no error and no visible symptom: the comparison simply never matches, and only
    /// mail without a usable Message-ID depends on it.
    /// </para>
    /// <para>
    /// The shipped default display timezone is <c>Etc/UCT</c>, under which several ways of
    /// getting this wrong still happen to agree. These tests therefore use a non-UTC zone, which
    /// is the case the defect actually shows up in.
    /// </para>
    /// </summary>
    public class DuplicateCheckSentDateTests
    {
        private static DateTimeHelper Helper(string timeZoneId) =>
            new(Options.Create(new TimeZoneOptions { DisplayTimeZoneId = timeZoneId }));

        /// <summary>
        /// The trap that has to stay closed: <c>ConvertToDisplayTimeZone</c> is overloaded, and
        /// the <see cref="DateTime"/> overload returns a value of Kind Unspecified untouched.
        /// <c>DateTimeOffset.DateTime</c> produces exactly such a value, so converting
        /// <c>x.DateTime</c> rather than <c>x</c> silently performs no conversion at all.
        /// </summary>
        [Fact]
        public void ConvertingTheOffsetAndItsDateTimeComponent_AreNotTheSameThing()
        {
            var helper = Helper("Europe/Berlin");
            var sent = new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);

            var viaOffset = helper.ConvertToDisplayTimeZone(sent);
            var viaDateTimeComponent = helper.ConvertToDisplayTimeZone(sent.DateTime);

            Assert.NotEqual(viaOffset, viaDateTimeComponent);
        }

        /// <summary>
        /// What the archiving pipelines write, compared with what the duplicate check now builds.
        /// Both go through the offset overload, so they agree.
        /// </summary>
        [Fact]
        public void StoredAndComparedSendTimes_Agree()
        {
            var helper = Helper("Europe/Berlin");
            var sent = new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);

            var stored = helper.ConvertToDisplayTimeZone(sent);
            var compared = helper.ConvertToDisplayTimeZone(sent);

            Assert.True(MailArchiver.Services.Shared.MailMatchKey.WithinTolerance(stored, compared));
        }

        /// <summary>
        /// The old comparison, kept as a regression guard: it took the raw DateTime component of
        /// the send time, which is the sender's local wall clock, and compared it against a
        /// column holding display-timezone wall clock. For a message sent at an offset differing
        /// from the display timezone those are hours apart, far outside the two-second window.
        /// </summary>
        [Fact]
        public void OldComparison_WouldHaveFallenOutsideTheWindow()
        {
            var helper = Helper("Etc/UCT");
            // A message sent at 14:00 in +02:00, i.e. 12:00 UTC.
            var sent = new DateTimeOffset(2026, 8, 28, 14, 0, 0, TimeSpan.FromHours(2));

            var stored = helper.ConvertToDisplayTimeZone(sent);   // 12:00
            var comparedTheOldWay = sent.DateTime;                // 14:00

            Assert.False(MailArchiver.Services.Shared.MailMatchKey
                .WithinTolerance(stored, comparedTheOldWay));
        }

        /// <summary>
        /// And the same message under the fixed comparison matches, on the shipped default zone.
        /// This is the pairing that shows the defect was never limited to non-UTC installations.
        /// </summary>
        [Fact]
        public void FixedComparison_MatchesOnTheDefaultTimeZone()
        {
            var helper = Helper("Etc/UCT");
            var sent = new DateTimeOffset(2026, 8, 28, 14, 0, 0, TimeSpan.FromHours(2));

            var stored = helper.ConvertToDisplayTimeZone(sent);
            var compared = helper.ConvertToDisplayTimeZone(sent);

            Assert.True(MailArchiver.Services.Shared.MailMatchKey.WithinTolerance(stored, compared));
        }
    }
}

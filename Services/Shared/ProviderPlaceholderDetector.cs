using System.Text.RegularExpressions;
using MimeKit;

namespace MailArchiver.Services.Shared
{
    /// <summary>
    /// Recognises the MIME document a mail provider substitutes for a message it cannot itself
    /// convert to MIME. Observed on on-premises Microsoft Exchange, which answers a BODY[] fetch
    /// for such a message with a generated "Retrieval using the IMAP4 protocol failed" mail
    /// instead of the original.
    ///
    /// Archiving that document is the honest outcome — it is the best representation the server is
    /// able to expose — but it must be recognisable afterwards, so an operator can tell
    /// "the archive holds the original" apart from "the archive holds the server's apology for it".
    ///
    /// Detection is deliberately conservative: the subject marker AND a body marker must both be
    /// present. The sender is never required, because the string differs between Exchange versions
    /// and localisations, and a subject alone would misclassify a genuine mail that merely quotes
    /// the error text.
    ///
    /// This class is pure (no I/O, no static state) so it can be unit-tested in isolation.
    /// </summary>
    public static class ProviderPlaceholderDetector
    {
        /// <summary>
        /// Subject prefix Exchange uses. The trailing message number varies, so this is a prefix
        /// test, not an equality test.
        /// </summary>
        private const string SubjectMarker =
            "Retrieval using the IMAP4 protocol failed for the following message:";

        /// <summary>
        /// Body markers, apostrophe-normalised. Both the contracted and the spelled-out form are
        /// accepted because the wording differs between Exchange builds.
        /// </summary>
        private static readonly string[] BodyMarkers =
        {
            "the server couldn't retrieve the following message",
            "the server could not retrieve the following message"
        };

        /// <summary>
        /// Pulls the original subject out of the placeholder body, which quotes it on its own line
        /// as <c>Subject: "..."</c>. Only for logging, so a miss is not an error.
        /// </summary>
        /// <remarks>
        /// The trailing <c>\r?</c> is load-bearing: MimeKit's <see cref="MimeKit.TextPart.Text"/>
        /// decoder normalises line endings to the running platform's <c>Environment.NewLine</c>,
        /// so on Windows the body carries CRLF. With <see cref="RegexOptions.Multiline"/>, .NET's
        /// <c>$</c> matches only before <c>\n</c> and not before <c>\r\n</c> — without the optional
        /// carriage return the pattern would not match at all on Windows deployments.
        /// </remarks>
        private static readonly Regex OriginalSubjectPattern = new(
            "^[ \\t]*Subject:[ \\t]*\"(?<subject>.*)\"[ \\t]*\\r?$",
            RegexOptions.Multiline | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        /// <summary>
        /// True when the message looks like a provider-generated retrieval error rather than real
        /// mail. Both the subject and the body have to say so.
        /// </summary>
        public static bool IsProviderRetrievalErrorPlaceholder(MimeMessage? message)
        {
            if (message == null)
                return false;

            var subject = NormalizeApostrophes(message.Subject);
            if (string.IsNullOrWhiteSpace(subject))
                return false;

            if (!subject.TrimStart().StartsWith(SubjectMarker, StringComparison.OrdinalIgnoreCase))
                return false;

            var body = NormalizeApostrophes(message.TextBody);
            if (string.IsNullOrWhiteSpace(body))
                return false;

            foreach (var marker in BodyMarkers)
            {
                if (body.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// The subject of the message the provider could not deliver, as quoted inside the
        /// placeholder body, or null when it cannot be read cheaply. Logging only — the archived
        /// message is never rewritten with it.
        /// </summary>
        public static string? TryGetOriginalSubject(MimeMessage? message)
        {
            var body = message?.TextBody;
            if (string.IsNullOrWhiteSpace(body))
                return null;

            var match = OriginalSubjectPattern.Match(body);
            if (!match.Success)
                return null;

            var subject = match.Groups["subject"].Value.Trim();
            return string.IsNullOrEmpty(subject) ? null : subject;
        }

        /// <summary>
        /// Exchange writes the typographic apostrophe in "couldn't". Fold it onto the ASCII one so
        /// a single marker matches both.
        /// </summary>
        private static string NormalizeApostrophes(string? value)
            => string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace('\u2019', '\'').Replace('\u02BC', '\'');
    }
}

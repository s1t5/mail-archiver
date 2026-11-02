namespace MailArchiver.Models
{
    /// <summary>
    /// Storage of the original message (the complete .eml) next to the parsed fields,
    /// bound from the "RawMessages" section. Off by default: it roughly doubles the
    /// storage per email, because attachments are kept a second time inside the message.
    /// </summary>
    public class RawMessageOptions
    {
        public const string RawMessages = "RawMessages";

        /// <summary>Master switch. When false, no original messages are stored.</summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Messages larger than this (uncompressed, in MB) are archived without their
        /// original. 0 = no limit.
        /// </summary>
        public int MaxMessageSizeMb { get; set; } = 50;

        /// <summary>
        /// Whether Microsoft Graph (M365) accounts store the original too. Costs one
        /// additional Graph request per message, which counts against the throttling budget.
        /// </summary>
        public bool IncludeGraph { get; set; } = true;
    }
}

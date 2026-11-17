using MailArchiver.Models;
using Microsoft.Extensions.Logging;

namespace MailArchiver.Services.Shared
{
    /// <summary>
    /// Logging for the cases in which an archived email ends up without its original message.
    /// A missing original is a compliance-relevant defect, so every path reports it at error
    /// level with the email id and the reason. Without this, support only sees the generic
    /// "error saving archived email" line and cannot tell whether the mail or its original
    /// failed, or why.
    /// </summary>
    public static class RawMessageLogging
    {
        /// <summary>
        /// Reports a capture that was refused before it was written: storage disabled (no log),
        /// no usable capture from the provider, or a message above the configured size limit.
        /// </summary>
        public static void LogNotStored(
            ILogger logger,
            int emailId,
            string? subject,
            RawMessageCapture? capture,
            RawMessageOptions options)
        {
            if (!options.Enabled)
            {
                return;
            }

            if (capture == null || capture.Bytes.Length == 0)
            {
                logger.LogError(
                    "Original message not stored for email {EmailId} ({Subject}): RawMessages is enabled " +
                    "but the provider delivered no usable capture. The email was archived without its original.",
                    emailId, subject);
                return;
            }

            var sizeMb = capture.Bytes.LongLength / 1024d / 1024d;
            logger.LogError(
                "Original message not stored for email {EmailId} ({Subject}): the message is {SizeMb:F2} MB " +
                "and exceeds RawMessages:MaxMessageSizeMb={LimitMb} MB. The email was archived without its original.",
                emailId, subject, sizeMb, options.MaxMessageSizeMb);
        }

        /// <summary>
        /// Reports a write failure after the email row was already committed. The mail stays in
        /// the archive without an original, and a later sync skips it as a duplicate, so this
        /// line is the only place the gap becomes visible.
        /// </summary>
        public static void LogPersistFailure(
            ILogger logger,
            Exception exception,
            int emailId,
            string? subject,
            string storageType)
        {
            logger.LogError(
                exception,
                "Original message not stored for email {EmailId} ({Subject}): writing to the {StorageType} " +
                "backend failed. The email was archived without its original; a re-sync skips it as a duplicate.",
                emailId, subject, storageType);
        }
    }
}

using MailArchiver.Models;
using MailArchiver.Services.Shared;
using MimeKit;
using XstReader;

namespace MailArchiver.Services.Providers.Pst
{
    /// <summary>
    /// Reads a PST/OST file using XstReader.Api, converts each message to a MimeMessage,
    /// and invokes the provided handler (MailImporter) per message.
    /// Optionally preserves PST folder names as archive folder names.
    /// </summary>
    public class PstProcessor
    {
        private readonly ILogger<PstProcessor> _logger;

        public PstProcessor(ILogger<PstProcessor> logger)
        {
            _logger = logger;
        }

        public async Task ProcessPstFile(
            PstImportJob job,
            MailAccount targetAccount,
            CancellationToken ct,
            Func<MimeMessage, string, Task<ImportResult>> handler)
        {
            using var xstFile = new XstFile(job.FilePath);
            await ProcessFolder(xstFile.RootFolder, job, handler, ct, isRoot: true);

            try { if (File.Exists(job.FilePath)) File.Delete(job.FilePath); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete temporary PST file"); }
        }

        private async Task ProcessFolder(
            XstFolder folder,
            PstImportJob job,
            Func<MimeMessage, string, Task<ImportResult>> handler,
            CancellationToken ct,
            bool isRoot = false)
        {
            ct.ThrowIfCancellationRequested();

            var folderDisplayName = folder.DisplayName ?? string.Empty;

            if (!isRoot && IsNonMailFolder(folderDisplayName))
            {
                _logger.LogDebug("Job {JobId}: Skipping non-mail folder '{Folder}'", job.JobId, folderDisplayName);
            }
            else
            {
                string archiveFolder = ResolveArchiveFolder(folderDisplayName, job, isRoot);
                job.CurrentFolder = isRoot ? "(root)" : folderDisplayName;

                foreach (var msg in folder.Messages)
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        var mimeMessage = ConvertToMimeMessage(msg);
                        job.CurrentEmailSubject = mimeMessage.Subject;

                        var result = await handler(mimeMessage, archiveFolder);

                        if (result.Success) job.SuccessCount++;
                        else if (result.AlreadyExists) job.SkippedAlreadyExistsCount++;
                        else job.FailedCount++;

                        job.ProcessedEmails++;
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        job.FailedCount++;
                        job.ProcessedEmails++;
                        _logger.LogWarning(ex, "Job {JobId}: Failed to import message from PST folder '{Folder}'",
                            job.JobId, folderDisplayName);
                    }
                }
            }

            foreach (var sub in folder.Folders)
            {
                ct.ThrowIfCancellationRequested();
                await ProcessFolder(sub, job, handler, ct, isRoot: false);
            }
        }

        private static string ResolveArchiveFolder(string folderDisplayName, PstImportJob job, bool isRoot)
        {
            if (!job.PreserveFolderStructure || isRoot)
                return job.TargetFolder;

            // Map common Outlook folder display names to standard archive folder names
            return folderDisplayName switch
            {
                "Inbox" or "INBOX" => "INBOX",
                "Sent Items" or "Sent" => "Sent",
                "Deleted Items" or "Trash" => "Trash",
                "Drafts" => "Drafts",
                "Junk Email" or "Spam" or "Junk" => "Junk",
                "Outbox" => "Outbox",
                _ => folderDisplayName
            };
        }

        private static bool IsNonMailFolder(string? name) =>
            name is "Calendar" or "Contacts" or "Tasks" or "Notes" or "Journal"
                 or "Suggested Contacts" or "Quick Step Settings" or "Conversation Action Settings"
                 or "Sync Issues" or "Conflicts" or "Local Failures" or "Server Failures"
                 or "RSS Feeds" or "RSS Subscriptions" or "Sharing" or "Yammer Root";

        private MimeMessage ConvertToMimeMessage(XstMessage msg)
        {
            var mime = new MimeMessage();

            // Message-ID
            var msgId = msg.InternetMessageId;
            mime.MessageId = !string.IsNullOrWhiteSpace(msgId)
                ? msgId.Trim('<', '>')
                : $"{Guid.NewGuid()}@pst-import";

            // Date
            if (msg.Date.HasValue)
                mime.Date = new DateTimeOffset(DateTime.SpecifyKind(msg.Date.Value, DateTimeKind.Utc));
            else
                mime.Date = DateTimeOffset.UtcNow;

            mime.Subject = msg.Subject ?? string.Empty;

            TryAddAddress(mime.From, msg.From);
            TryAddAddressList(mime.To, msg.To);
            TryAddAddressList(mime.Cc, msg.Cc);
            TryAddAddressList(mime.Bcc, msg.Bcc);

            var builder = new BodyBuilder();

            if (msg.Body != null)
            {
                if (msg.Body.Format == XstMessageBodyFormat.Html)
                    builder.HtmlBody = msg.Body.Text;
                else
                    // PlainText and RTF both go in TextBody
                    builder.TextBody = msg.Body.Text;
            }

            foreach (var att in msg.AttachmentsFiles)
            {
                try
                {
                    using var ms = new MemoryStream();
                    att.SaveToStream(ms);
                    var name = att.FileNameForSaving ?? att.LongFileName ?? att.FileName ?? "attachment";
                    builder.Attachments.Add(name, ms.ToArray());
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read PST attachment '{Name}'", att.LongFileName ?? att.FileName);
                }
            }

            mime.Body = builder.ToMessageBody();
            return mime;
        }

        private static void TryAddAddress(InternetAddressList list, string? address)
        {
            if (string.IsNullOrWhiteSpace(address)) return;
            try { list.Add(MailboxAddress.Parse(address)); }
            catch { list.Add(new MailboxAddress(address, string.Empty)); }
        }

        private static void TryAddAddressList(InternetAddressList list, string? addresses)
        {
            if (string.IsNullOrWhiteSpace(addresses)) return;
            try
            {
                list.AddRange(InternetAddressList.Parse(addresses));
            }
            catch
            {
                foreach (var part in addresses.Split([';', ','], StringSplitOptions.RemoveEmptyEntries))
                {
                    var trimmed = part.Trim();
                    if (string.IsNullOrEmpty(trimmed)) continue;
                    try { list.Add(MailboxAddress.Parse(trimmed)); }
                    catch { list.Add(new MailboxAddress(trimmed, string.Empty)); }
                }
            }
        }
    }
}

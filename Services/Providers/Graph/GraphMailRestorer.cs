using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services.Shared;
using MailArchiver.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace MailArchiver.Services.Providers.Graph
{
    /// <summary>
    /// Restores archived emails back to a Microsoft Graph (M365) mailbox, including
    /// attachment restoration, inline image processing, and folder hierarchy resolution.
    /// </summary>
    public class GraphMailRestorer
    {
        private readonly GraphAuthClientFactory _authFactory;
        private readonly IGraphFolderService _folderService;
        private readonly MailArchiverDbContext _context;
        private readonly ILogger<GraphMailRestorer> _logger;
        private readonly DateTimeHelper _dateTimeHelper;

        public GraphMailRestorer(
            GraphAuthClientFactory authFactory,
            IGraphFolderService folderService,
            MailArchiverDbContext context,
            ILogger<GraphMailRestorer> logger,
            DateTimeHelper dateTimeHelper)
        {
            _authFactory = authFactory;
            _folderService = folderService;
            _context = context;
            _logger = logger;
            _dateTimeHelper = dateTimeHelper;
        }

        /// <summary>
        /// Restores a single archived email to a specific folder in the target M365 mailbox.
        /// </summary>
        public async Task<bool> RestoreEmailToFolderAsync(
            ArchivedEmail email,
            MailAccount targetAccount,
            string folderName,
            bool preserveFolderStructure = false)
        {
            _logger.LogInformation("RestoreEmailToFolderAsync via Graph API called for email {EmailId} to folder {FolderName}, preserveFolderStructure={Preserve}",
                email.Id, folderName, preserveFolderStructure);

            try
            {
                var graphClient = _authFactory.CreateGraphClient(targetAccount);
                var folders = await _folderService.GetAllMailFoldersAsync(graphClient, targetAccount.EmailAddress);

                // Determine the desired full target path
                string targetFolderPath;
                if (preserveFolderStructure)
                {
                    var originalFolderName = email.FolderName ?? "INBOX";
                    targetFolderPath = string.IsNullOrWhiteSpace(folderName)
                        ? originalFolderName
                        : $"{folderName.Trim().TrimEnd('/', '\\')}/{originalFolderName.Trim().TrimStart('/', '\\')}";

                    _logger.LogInformation("Preserving folder structure: original folder '{OriginalFolder}' -> target '{TargetPath}'",
                        originalFolderName, targetFolderPath);
                }
                else
                {
                    targetFolderPath = folderName;
                }

                // Resolve / create the folder hierarchy
                MailFolder? targetFolder = await _folderService.EnsureFolderPathAsync(
                    graphClient,
                    targetAccount.EmailAddress,
                    targetFolderPath,
                    folders,
                    createIfMissing: preserveFolderStructure);

                if (targetFolder == null && preserveFolderStructure)
                {
                    _logger.LogWarning("Could not create folder structure for {TargetPath}, falling back to base folder {FolderName}",
                        targetFolderPath, folderName);
                    targetFolder = await _folderService.EnsureFolderPathAsync(
                        graphClient,
                        targetAccount.EmailAddress,
                        folderName,
                        folders,
                        createIfMissing: true);
                }

                if (targetFolder == null)
                {
                    targetFolder = await _folderService.GetWellKnownInboxAsync(graphClient, targetAccount.EmailAddress, folders);
                    if (targetFolder == null)
                    {
                        _logger.LogError("Could not find target folder {FolderName} or Inbox for account {AccountName}",
                            targetFolderPath, targetAccount.Name);
                        return false;
                    }
                    _logger.LogWarning("Target folder {FolderName} not found, using Inbox instead", targetFolderPath);
                }

                // Process the HTML body to ensure inline images are properly referenced
                var processedHtmlBody = email.HtmlBody;
                if (!string.IsNullOrEmpty(email.HtmlBody) && email.Attachments != null && email.Attachments.Any(a => !string.IsNullOrEmpty(a.ContentId)))
                {
                    processedHtmlBody = MailContentHelper.ProcessHtmlBodyForInlineImages(email.HtmlBody, email.Attachments);
                }

                // Create the message to restore
                var message = new Message
                {
                    Subject = email.Subject ?? "(No Subject)",
                    Body = new ItemBody
                    {
                        ContentType = !string.IsNullOrEmpty(processedHtmlBody) ? BodyType.Html : BodyType.Text,
                        Content = !string.IsNullOrEmpty(processedHtmlBody) ? processedHtmlBody : (email.Body ?? "(No Content)")
                    },
                    From = new Recipient
                    {
                        EmailAddress = new Microsoft.Graph.Models.EmailAddress
                        {
                            Address = email.From ?? "unknown@unknown.com",
                            Name = email.From ?? "Unknown Sender"
                        }
                    },
                    ToRecipients = ParseEmailAddresses(email.To),
                    CcRecipients = ParseEmailAddresses(email.Cc),
                    BccRecipients = ParseEmailAddresses(email.Bcc),
                    SentDateTime = email.SentDate,
                    ReceivedDateTime = email.SentDate,
                    InternetMessageId = email.MessageId,
                    IsRead = false,
                    Importance = Importance.Normal,
                    InferenceClassification = InferenceClassificationType.Focused,
                    SingleValueExtendedProperties = new List<SingleValueLegacyExtendedProperty>
                    {
                        new SingleValueLegacyExtendedProperty
                        {
                            Id = "Integer 0x0E07", // PidTagMessageFlags
                            Value = "1" // Mark as not a draft (MSGFLAG_READ)
                        },
                        new SingleValueLegacyExtendedProperty
                        {
                            Id = "SystemTime 0x0039", // PidTagClientSubmitTime -> SentDateTime
                            Value = NormalizeToUtcIso8601(email.SentDate)
                        },
                        new SingleValueLegacyExtendedProperty
                        {
                            Id = "SystemTime 0x0E06", // PidTagMessageDeliveryTime
                            Value = NormalizeToUtcIso8601(email.SentDate)
                        }
                    }
                };

                _logger.LogDebug("Creating message with Subject: {Subject}, From: {From}, To: {To}, Body length: {BodyLength}",
                    message.Subject, message.From?.EmailAddress?.Address,
                    string.Join(", ", message.ToRecipients?.Select(r => r.EmailAddress?.Address) ?? new List<string>()),
                    message.Body?.Content?.Length ?? 0);

                var createdMessage = await graphClient.Users[targetAccount.EmailAddress].MailFolders[targetFolder.Id].Messages.PostAsync(message);

                if (createdMessage != null)
                {
                    _logger.LogInformation("Message created successfully with ID: {MessageId}", createdMessage.Id);

                    if (email.Attachments != null && email.Attachments.Any())
                    {
                        await RestoreAttachmentsAsync(graphClient, email, targetAccount, createdMessage.Id);
                    }

                    _logger.LogInformation("Successfully restored email {EmailId} to folder {FolderName} via Graph API.", 
                        email.Id, folderName);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring email {EmailId} to folder {FolderName} via Graph API: {Message}",
                    email.Id, folderName, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Restores multiple emails with progress tracking.
        /// </summary>
        public async Task<(int Successful, int Failed)> RestoreMultipleEmailsWithProgressAsync(
            List<int> emailIds,
            int targetAccountId,
            string folderName,
            bool preserveFolderStructure,
            Action<int, int, int> progressCallback,
            CancellationToken cancellationToken = default)
        {
            int successCount = 0;
            int failCount = 0;

            var targetAccount = await _context.MailAccounts.FindAsync(targetAccountId);
            if (targetAccount == null)
            {
                _logger.LogError("Target account with ID {AccountId} not found", targetAccountId);
                return (0, emailIds.Count);
            }

            foreach (var emailId in emailIds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var email = await _context.ArchivedEmails
                        .Include(e => e.Attachments)
                        .FirstOrDefaultAsync(e => e.Id == emailId, cancellationToken);

                    if (email == null)
                    {
                        _logger.LogWarning("Email with ID {EmailId} not found during batch restore", emailId);
                        failCount++;
                        continue;
                    }

                    var result = await RestoreEmailToFolderAsync(email, targetAccount, folderName, preserveFolderStructure);
                    if (result)
                        successCount++;
                    else
                        failCount++;
                }
                catch (Exception ex)
                {
                    failCount++;
                    _logger.LogError(ex, "Error restoring email {EmailId} to account {AccountId}", emailId, targetAccountId);
                }

                progressCallback?.Invoke(successCount + failCount, successCount, failCount);
            }

            return (successCount, failCount);
        }

        /// <summary>
        /// Restores attachments for a newly created message.
        /// </summary>
        private async Task RestoreAttachmentsAsync(
            GraphServiceClient graphClient,
            ArchivedEmail email,
            MailAccount targetAccount,
            string messageId)
        {
            try
            {
                foreach (var attachment in email.Attachments!)
                {
                    try
                    {
                        var fileAttachment = new Microsoft.Graph.Models.FileAttachment
                        {
                            OdataType = "#microsoft.graph.fileAttachment",
                            Name = attachment.FileName,
                            ContentType = attachment.ContentType,
                            ContentBytes = attachment.Content
                        };

                        if (!string.IsNullOrEmpty(attachment.ContentId))
                        {
                            var contentId = attachment.ContentId;
                            if (contentId.StartsWith("<") && contentId.EndsWith(">"))
                            {
                                contentId = contentId.Trim('<', '>');
                            }
                            fileAttachment.ContentId = contentId;
                        }

                        await graphClient.Users[targetAccount.EmailAddress]
                            .Messages[messageId]
                            .Attachments
                            .PostAsync(fileAttachment);

                        _logger.LogInformation("Successfully restored attachment {AttachmentName} for email {EmailId}",
                            attachment.FileName, email.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error restoring attachment {AttachmentName} for email {EmailId}",
                            attachment.FileName, email.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring attachments for email {EmailId}", email.Id);
            }
        }

        /// <summary>
        /// Parses a comma-separated string of email addresses into a list of Graph Recipients.
        /// </summary>
        private static List<Recipient> ParseEmailAddresses(string? emailAddresses)
        {
            var recipients = new List<Recipient>();

            if (string.IsNullOrEmpty(emailAddresses))
                return recipients;

            var addresses = emailAddresses.Split(',', StringSplitOptions.RemoveEmptyEntries);

            foreach (var address in addresses)
            {
                var cleanAddress = address.Trim();
                if (!string.IsNullOrEmpty(cleanAddress))
                {
                    recipients.Add(new Recipient
                    {
                        EmailAddress = new Microsoft.Graph.Models.EmailAddress
                        {
                            Address = cleanAddress,
                            Name = cleanAddress
                        }
                    });
                }
            }

            return recipients;
        }

        /// <summary>
        /// Normalizes a DateTime value to a UTC ISO-8601 string for MAPI extended properties.
        /// Converts from the configured display timezone back to UTC.
        /// </summary>
        private string NormalizeToUtcIso8601(DateTime value)
        {
            var utc = _dateTimeHelper.ConvertFromDisplayTimeZoneToUtc(value);
            return utc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Models.ViewModels;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Search;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace MailArchiver.Services
{
    public class EmailService : IEmailService
    {
        private readonly MailArchiverDbContext _context;
        private readonly ILogger<EmailService> _logger;

        public EmailService(MailArchiverDbContext context, ILogger<EmailService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task SyncMailAccountAsync(MailAccount account)
        {
            _logger.LogInformation("Starting sync for account: {AccountName}", account.Name);

            using var client = new ImapClient();

            try
            {
                await client.ConnectAsync(account.ImapServer, account.ImapPort, account.UseSSL);
                await client.AuthenticateAsync(account.Username, account.Password);

                _logger.LogInformation("Connected to IMAP server for {AccountName}", account.Name);

                // Prepare a list to store all folders
                var allFolders = new List<IMailFolder>();

                // Get personal namespace folders - main folders like Inbox, Sent, etc.
                foreach (var folder in client.GetFolders(client.PersonalNamespaces[0]))
                {
                    allFolders.Add(folder);

                    // Recursively add all subfolders
                    await AddSubfoldersRecursively(folder, allFolders);
                }

                _logger.LogInformation("Found {Count} folders for account: {AccountName}",
                    allFolders.Count, account.Name);

                // Process each folder
                foreach (var folder in allFolders)
                {
                    try
                    {
                        await SyncFolderAsync(folder, account);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error syncing folder {FolderName} for account {AccountName}: {Message}",
                            folder.FullName, account.Name, ex.Message);
                    }
                }

                // Update lastSync
                account.LastSync = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                await client.DisconnectAsync(true);

                _logger.LogInformation("Sync completed for account: {AccountName}", account.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during sync for account {AccountName}: {Message}",
                    account.Name, ex.Message);
                throw;
            }
        }

        private async Task SyncFolderAsync(IMailFolder folder, MailAccount account)
        {
            _logger.LogInformation("Syncing folder: {FolderName} for account: {AccountName}",
                folder.FullName, account.Name);

            try
            {
                // Skip some special system folders that might cause issues
                if (folder.Attributes.HasFlag(FolderAttributes.NonExistent) ||
                    folder.Attributes.HasFlag(FolderAttributes.NoSelect))
                {
                    _logger.LogInformation("Skipping folder {FolderName} (non-existent or non-selectable)",
                        folder.FullName);
                    return;
                }

                await folder.OpenAsync(FolderAccess.ReadOnly);

                // Determine if this is an outgoing mail folder (like Sent Items)
                bool isOutgoing = IsOutgoingFolder(folder);

                // Get messages newer than last sync
                var lastSync = account.LastSync;
                var query = SearchQuery.DeliveredAfter(lastSync);
                var uids = await folder.SearchAsync(query);

                _logger.LogInformation("Found {Count} new messages in folder {FolderName} for account: {AccountName}",
                    uids.Count, folder.FullName, account.Name);

                foreach (var uid in uids)
                {
                    try
                    {
                        var message = await folder.GetMessageAsync(uid);
                        await ArchiveEmailAsync(account, message, isOutgoing, folder.FullName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error archiving message from folder {FolderName}: {Message}",
                            folder.FullName, ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing folder {FolderName}: {Message}",
                    folder.FullName, ex.Message);
            }
        }

        private bool IsOutgoingFolder(IMailFolder folder)
        {
            // Common names for sent folders in different languages and email systems
            var sentFolderNames = new[]
            {
        "sent", "sent items", "sent mail", "gesendet", "gesendete objekte",
        "gesendete", "enviado", "envoyé", "inviato", "verzonden"
    };

            string folderNameLower = folder.Name.ToLowerInvariant();

            // Check if folder name contains any of the common sent folder names
            return sentFolderNames.Any(name => folderNameLower.Contains(name)) ||
                   // Or check if the folder has the Sent flag
                   folder.Attributes.HasFlag(FolderAttributes.Sent);
        }

        private async Task AddSubfoldersRecursively(IMailFolder folder, List<IMailFolder> allFolders)
        {
            try
            {
                // Get all subfolders of the current folder
                var subfolders = folder.GetSubfolders(false);

                foreach (var subfolder in subfolders)
                {
                    allFolders.Add(subfolder);

                    // Recursively add this folder's subfolders
                    await AddSubfoldersRecursively(subfolder, allFolders);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error retrieving subfolders for {FolderName}: {Message}",
                    folder.FullName, ex.Message);
            }
        }

        private async Task<IMailFolder> GetSentFolderAsync(ImapClient client)
        {
            // Versuchen, den Ordner für gesendete Nachrichten zu finden
            // Verschiedene E-Mail-Anbieter verwenden unterschiedliche Namen
            string[] possibleNames = { "Sent", "Sent Items", "Gesendet", "Gesendete Objekte", "Sent Messages" };

            foreach (var name in possibleNames)
            {
                try
                {
                    var folder = await client.GetFolderAsync(name);
                    if (folder != null)
                        return folder;
                }
                catch
                {
                    // Ignorieren, wenn Ordner nicht existiert
                }
            }

            // Versuch, im Namespace nach Sent-Ordnern zu suchen
            foreach (var folder in client.GetFolders(client.PersonalNamespaces[0]))
            {
                if (folder.Name.ToLower().Contains("sent") || folder.Name.ToLower().Contains("gesend"))
                {
                    return folder;
                }
            }

            return null;
        }

        private async Task ArchiveEmailAsync(MailAccount account, MimeMessage message, bool isOutgoing, string? folderName = null)
        {
            // Check if this email is already archived
            var messageId = message.MessageId ??
                $"{message.From}-{message.To}-{message.Subject}-{message.Date.Ticks}";

            var existingEmail = await _context.ArchivedEmails
                .FirstOrDefaultAsync(e => e.MessageId == messageId && e.MailAccountId == account.Id);

            if (existingEmail != null)
                return;

            // Make sure the DateTime is in UTC
            DateTime sentDate = DateTime.SpecifyKind(message.Date.DateTime, DateTimeKind.Utc);

            var archivedEmail = new ArchivedEmail
            {
                MailAccountId = account.Id,
                MessageId = messageId,
                Subject = message.Subject ?? "(No Subject)",
                From = message.From.ToString(),
                To = message.To.ToString(),
                Cc = message.Cc?.ToString() ?? string.Empty,
                Bcc = message.Bcc?.ToString() ?? string.Empty,
                SentDate = sentDate,
                ReceivedDate = DateTime.UtcNow,
                IsOutgoing = isOutgoing,
                HasAttachments = message.Attachments.Any(),
                Body = message.TextBody ?? string.Empty,
                HtmlBody = message.HtmlBody ?? string.Empty,
                FolderName = folderName
            };

            _context.ArchivedEmails.Add(archivedEmail);
            await _context.SaveChangesAsync();

            // Anhänge speichern
            if (message.Attachments.Any())
            {
                foreach (var attachment in message.Attachments)
                {
                    if (attachment is MimePart mimePart)
                    {
                        try
                        {
                            using var ms = new MemoryStream();
                            await mimePart.Content.DecodeToAsync(ms);

                            var emailAttachment = new EmailAttachment
                            {
                                ArchivedEmailId = archivedEmail.Id,
                                FileName = mimePart.FileName ?? "attachment.dat",
                                ContentType = mimePart.ContentType?.MimeType ?? "application/octet-stream",
                                Content = ms.ToArray(),
                                Size = ms.Length
                            };

                            _context.EmailAttachments.Add(emailAttachment);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error saving attachment: {FileName}", mimePart.FileName);
                        }
                    }
                }
                await _context.SaveChangesAsync();
            }

            _logger.LogInformation(
                "Archived email: {Subject}, From: {From}, To: {To}, Account: {AccountName}",
                archivedEmail.Subject, archivedEmail.From, archivedEmail.To, account.Name);
        }

        public async Task<(List<ArchivedEmail> Emails, int TotalCount)> SearchEmailsAsync(
            string searchTerm,
            DateTime? fromDate,
            DateTime? toDate,
            int? accountId,
            bool? isOutgoing,
            int skip,
            int take)
        {
            var query = _context.ArchivedEmails
                .Include(e => e.MailAccount)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(e =>
                    e.Subject.Contains(searchTerm) ||
                    e.Body.Contains(searchTerm) ||
                    e.From.Contains(searchTerm) ||
                    e.To.Contains(searchTerm));
            }

            if (fromDate.HasValue)
                query = query.Where(e => e.SentDate >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(e => e.SentDate <= toDate.Value.AddDays(1).AddSeconds(-1)); // Bis Ende des Tages

            if (accountId.HasValue)
                query = query.Where(e => e.MailAccountId == accountId.Value);

            if (isOutgoing.HasValue)
                query = query.Where(e => e.IsOutgoing == isOutgoing.Value);

            var totalCount = await query.CountAsync();

            var emails = await query
                .OrderByDescending(e => e.SentDate)
                .Skip(skip)
                .Take(take)
                .ToListAsync();

            return (emails, totalCount);
        }

        // In EmailService.cs, look for the ExportEmailsAsync method
        public async Task<byte[]> ExportEmailsAsync(ExportViewModel parameters)
        {
            var (emails, _) = await SearchEmailsAsync(
                parameters.SearchTerm,
                parameters.FromDate,
                parameters.ToDate,
                parameters.SelectedAccountId,
                parameters.IsOutgoing,
                0,
                10000); // Limit to 10,000 emails

            using var ms = new MemoryStream();

            switch (parameters.Format)
            {
                case ExportFormat.Csv:
                    using (var writer = new StreamWriter(ms, Encoding.UTF8, leaveOpen: true))
                    {
                        // CSV header
                        writer.WriteLine("Subject;From;To;Date;Account;Direction;Message Text");

                        // CSV data
                        foreach (var email in emails)
                        {
                            var subject = email.Subject.Replace("\"", "\"\"").Replace(";", ",");
                            var from = email.From.Replace("\"", "\"\"").Replace(";", ",");
                            var to = email.To.Replace("\"", "\"\"").Replace(";", ",");
                            var sentDate = email.SentDate.ToString("yyyy-MM-dd HH:mm:ss");
                            var account = email.MailAccount?.Name.Replace("\"", "\"\"").Replace(";", ",") ?? "Unknown";
                            var direction = email.IsOutgoing ? "Outgoing" : "Incoming";
                            var body = email.Body?.Replace("\r", " ").Replace("\n", " ")
                                .Replace("\"", "\"\"").Replace(";", ",") ?? "";

                            writer.WriteLine($"\"{subject}\";\"{from}\";\"{to}\";\"{sentDate}\";\"{account}\";\"{direction}\";\"{body}\"");
                        }

                        writer.Flush(); // Make sure all data is written
                    }
                    break;

                case ExportFormat.Json:
                    var exportData = emails.Select(e => new
                    {
                        Subject = e.Subject,
                        From = e.From,
                        To = e.To,
                        SentDate = e.SentDate,
                        AccountName = e.MailAccount?.Name,
                        IsOutgoing = e.IsOutgoing,
                        Body = e.Body
                    }).ToList();

                    var options = new JsonSerializerOptions { WriteIndented = true };
                    await JsonSerializer.SerializeAsync(ms, exportData, options);
                    await ms.FlushAsync(); // Make sure all data is written
                    break;

                case ExportFormat.Eml:
                    // Diese Implementierung ist für einzelne E-Mails
                    if (parameters.EmailId.HasValue)
                    {
                        var email = await _context.ArchivedEmails
                            .Include(e => e.Attachments)
                            .FirstOrDefaultAsync(e => e.Id == parameters.EmailId.Value);

                        if (email != null)
                        {
                            var message = new MimeMessage();
                            message.Subject = email.Subject;

                            try { message.From.Add(InternetAddress.Parse(email.From)); }
                            catch { message.From.Add(new MailboxAddress("Sender", "sender@example.com")); }

                            foreach (var to in email.To.Split(',', StringSplitOptions.RemoveEmptyEntries))
                            {
                                try { message.To.Add(InternetAddress.Parse(to.Trim())); }
                                catch { continue; }
                            }

                            if (!string.IsNullOrEmpty(email.Cc))
                            {
                                foreach (var cc in email.Cc.Split(',', StringSplitOptions.RemoveEmptyEntries))
                                {
                                    try { message.Cc.Add(InternetAddress.Parse(cc.Trim())); }
                                    catch { continue; }
                                }
                            }

                            var body = !string.IsNullOrEmpty(email.HtmlBody)
                                ? new TextPart("html") { Text = email.HtmlBody }
                                : new TextPart("plain") { Text = email.Body };

                            if (email.Attachments.Any())
                            {
                                var multipart = new Multipart("mixed");
                                multipart.Add(body);

                                foreach (var attachment in email.Attachments)
                                {
                                    var mimePart = new MimePart(attachment.ContentType)
                                    {
                                        Content = new MimeContent(new MemoryStream(attachment.Content)),
                                        ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                                        ContentTransferEncoding = ContentEncoding.Base64,
                                        FileName = attachment.FileName
                                    };

                                    multipart.Add(mimePart);
                                }

                                message.Body = multipart;
                            }
                            else
                            {
                                message.Body = body;
                            }

                            message.Date = email.SentDate;
                            message.MessageId = email.MessageId;

                            message.WriteTo(ms);
                        }
                    }
                    break;
            }

            ms.Position = 0;
            return ms.ToArray();
        }

        public async Task<DashboardViewModel> GetDashboardStatisticsAsync()
        {
            var model = new DashboardViewModel();

            // Basic statistics
            model.TotalEmails = await _context.ArchivedEmails.CountAsync();
            model.TotalAccounts = await _context.MailAccounts.CountAsync();
            model.TotalAttachments = await _context.EmailAttachments.CountAsync();

            var totalSizeBytes = await _context.EmailAttachments.SumAsync(a => (long)a.Size);
            model.TotalStorageUsed = FormatFileSize(totalSizeBytes);

            // Statistics per account
            model.EmailsPerAccount = await _context.MailAccounts
                .Select(a => new AccountStatistics
                {
                    AccountName = a.Name,
                    EmailAddress = a.EmailAddress,
                    EmailCount = a.ArchivedEmails.Count,
                    LastSyncTime = a.LastSync
                })
                .ToListAsync();

            // Emails per month (last 12 months)
            var startDate = DateTime.UtcNow.AddMonths(-11).Date;
            var months = new List<EmailCountByPeriod>();

            for (int i = 0; i < 12; i++)
            {
                var currentMonth = startDate.AddMonths(i);
                var nextMonth = currentMonth.AddMonths(1);

                // FIXED APPROACH - Use EF Core's built-in query instead of raw SQL
                var count = await _context.ArchivedEmails
                    .Where(e => e.SentDate >= currentMonth && e.SentDate < nextMonth)
                    .CountAsync();

                months.Add(new EmailCountByPeriod
                {
                    Period = $"{CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(currentMonth.Month)} {currentMonth.Year}",
                    Count = count
                });
            }

            model.EmailsByMonth = months;

            // Top senders
            model.TopSenders = await _context.ArchivedEmails
                .Where(e => !e.IsOutgoing)
                .GroupBy(e => e.From)
                .Select(g => new EmailCountByAddress
                {
                    EmailAddress = g.Key,
                    Count = g.Count()
                })
                .OrderByDescending(e => e.Count)
                .Take(10)
                .ToListAsync();

            // Recent emails
            model.RecentEmails = await _context.ArchivedEmails
                .Include(e => e.MailAccount)
                .OrderByDescending(e => e.ReceivedDate)
                .Take(10)
                .ToListAsync();

            return model;
        }

        private string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;

            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }

            return $"{number:n1} {suffixes[counter]}";
        }

        public async Task<bool> TestConnectionAsync(MailAccount account)
        {
            try
            {
                using var client = new ImapClient();
                await client.ConnectAsync(account.ImapServer, account.ImapPort, account.UseSSL);
                await client.AuthenticateAsync(account.Username, account.Password);
                await client.DisconnectAsync(true);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Connection test failed for account {AccountName}: {Message}",
                    account.Name, ex.Message);
                return false;
            }
        }
    }
}
using MailKit;
using MailKit.Net.Imap;

namespace MailArchiver.Services.Providers.Imap
{
    /// <summary>
    /// IMAP folder discovery service with robust multi-strategy fallback for different IMAP server implementations.
    /// Supports namespace-based folder listing, hybrid recursive/non-recursive retrieval,
    /// non-subscribed folder discovery, and alternative method as last resort.
    /// </summary>
    public class ImapFolderService : IImapFolderService
    {
        private readonly ILogger<ImapFolderService> _logger;

        public ImapFolderService(ILogger<ImapFolderService> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<List<IMailFolder>> GetAllFoldersAsync(ImapClient client, string accountName)
        {
            var allFolders = new List<IMailFolder>();

            try
            {
                _logger.LogInformation("Retrieving all folders from IMAP server for account: {AccountName}", accountName);

                // IMPORTANT: First always try to add INBOX explicitly
                try
                {
                    var inbox = client.Inbox;
                    if (inbox != null)
                    {
                        _logger.LogInformation("Adding INBOX explicitly: {FullName}", inbox.FullName);
                        if (!inbox.Attributes.HasFlag(FolderAttributes.NonExistent) &&
                            !inbox.Attributes.HasFlag(FolderAttributes.NoSelect))
                        {
                            allFolders.Add(inbox);
                        }
                    }
                }
                catch (Exception inboxEx)
                {
                    _logger.LogWarning(inboxEx, "Could not access INBOX for {AccountName}", accountName);
                }

                if (client.PersonalNamespaces != null && client.PersonalNamespaces.Count > 0)
                {
                    var ns = client.PersonalNamespaces[0];
                    _logger.LogDebug("Using PersonalNamespace: {Path}", ns.Path);

                    // New Hybrid folder retrieval
                    try
                    {
                        var rootFolders = await client.GetFoldersAsync(ns, StatusItems.None, subscribedOnly: false);
                        _logger.LogInformation("GetFoldersAsync(including non-subscribed) returned {Count} folders", rootFolders.Count);

                        foreach (var folder in rootFolders)
                        {
                            _logger.LogDebug("Found folder: Name={Name}, FullName={FullName}, Attributes={Attributes}",
                                folder.Name ?? "NULL", folder.FullName ?? "NULL", folder.Attributes);

                            if (!folder.Attributes.HasFlag(FolderAttributes.NonExistent) &&
                                !folder.Attributes.HasFlag(FolderAttributes.NoSelect) &&
                                !allFolders.Any(f => f.FullName == folder.FullName))
                            {
                                allFolders.Add(folder);
                            }
                        }
                    }
                    catch (Exception getFoldersEx)
                    {
                        _logger.LogWarning(getFoldersEx, "GetFoldersAsync(recursive) failed for {AccountName}, trying non-recursive fallback", accountName);

                        try
                        {
                            var toProcess = new Queue<IMailFolder>();

                            var topFolders = await client.GetFoldersAsync(ns, StatusItems.None, subscribedOnly: false);
                            _logger.LogInformation("Fallback: got {Count} top-level folders (including non-subscribed)", topFolders.Count);

                            foreach (var topFolder in topFolders)
                            {
                                _logger.LogDebug("Found top-level folder: Name={Name}, FullName={FullName}, Attributes={Attributes}",
                                    topFolder.Name ?? "NULL", topFolder.FullName ?? "NULL", topFolder.Attributes);

                                if (!topFolder.Attributes.HasFlag(FolderAttributes.NonExistent) &&
                                    !topFolder.Attributes.HasFlag(FolderAttributes.NoSelect) &&
                                    !allFolders.Any(f => f.FullName == topFolder.FullName))
                                {
                                    allFolders.Add(topFolder);
                                    toProcess.Enqueue(topFolder);
                                }
                            }

                            while (toProcess.Count > 0)
                            {
                                var currentFolder = toProcess.Dequeue();
                                try
                                {
                                    var subFolders = await currentFolder.GetSubfoldersAsync(false);
                                    foreach (var subFolder in subFolders)
                                    {
                                        _logger.LogDebug("Found subfolder: Name={Name}, FullName={FullName}, Attributes={Attributes}",
                                            subFolder.Name ?? "NULL", subFolder.FullName ?? "NULL", subFolder.Attributes);

                                        if (!subFolder.Attributes.HasFlag(FolderAttributes.NonExistent) &&
                                            !subFolder.Attributes.HasFlag(FolderAttributes.NoSelect) &&
                                            !allFolders.Any(f => f.FullName == subFolder.FullName))
                                        {
                                            allFolders.Add(subFolder);
                                            toProcess.Enqueue(subFolder);
                                        }
                                    }
                                }
                                catch (Exception subEx)
                                {
                                    _logger.LogWarning(subEx, "Could not get subfolders for {Folder}", currentFolder.FullName);
                                }
                            }
                        }
                        catch (Exception fallbackEx)
                        {
                            _logger.LogError(fallbackEx, "Fallback also failed for {AccountName}", accountName);
                        }
                    }

                    // If GetFoldersAsync returned 0 folders, use alternative method
                    if (allFolders.Count <= 1)
                    {
                        _logger.LogInformation("Few folders found via GetFoldersAsync, trying alternative folder discovery method for {AccountName}", accountName);

                        try
                        {
                            var rootFolder = await client.GetFolderAsync(ns.Path ?? string.Empty);
                            _logger.LogDebug("Got root folder: {FullName}", rootFolder.FullName);

                            await AddSubfoldersRecursivelySimple(rootFolder, allFolders);
                            _logger.LogInformation("Alternative method found {Count} additional folders", allFolders.Count - 1);
                        }
                        catch (Exception altEx)
                        {
                            _logger.LogWarning(altEx, "Alternative folder discovery method also failed for {AccountName}", accountName);
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("No PersonalNamespaces available for account {AccountName}", accountName);
                }

                _logger.LogInformation("Total selectable folders found for {AccountName}: {Count}", accountName, allFolders.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving folders for {AccountName}: {Message}", accountName, ex.Message);
            }

            return allFolders;
        }

        /// <summary>
        /// Simple recursive helper method for retrieving subfolders.
        /// Used as a last resort when modern IMAP methods fail.
        /// </summary>
        private async Task AddSubfoldersRecursivelySimple(IMailFolder folder, List<IMailFolder> allFolders)
        {
            try
            {
                var subfolders = folder.GetSubfolders(false);
                foreach (var subfolder in subfolders)
                {
                    if (!subfolder.Attributes.HasFlag(FolderAttributes.NonExistent) &&
                        !subfolder.Attributes.HasFlag(FolderAttributes.NoSelect))
                    {
                        allFolders.Add(subfolder);
                    }
                    await AddSubfoldersRecursivelySimple(subfolder, allFolders);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error retrieving subfolders for {FolderName}: {Message}",
                    folder.FullName, ex.Message);
            }
        }

        /// <inheritdoc/>
        public bool IsOutgoingFolder(IMailFolder folder)
        {
            var sentFolderNames = new[]
            {
                "المرسلة", "البريد المرسل",
                "изпратени", "изпратена поща",
                "已发送", "已传送",
                "poslano", "poslana pošta",
                "odeslané", "odeslaná pošta",
                "sendt", "sendte elementer",
                "verzonden", "verzonden items", "verzonden e-mail",
                "sent", "sent items", "sent mail",
                "saadetud", "saadetud kirjad",
                "lähetetyt", "lähetetyt kohteet",
                "envoyé", "éléments envoyés", "mail envoyé",
                "gesendet", "gesendete objekte", "gesendete",
                "απεσταλμένα", "σταλμένα", "σταλμένα μηνύματα",
                "נשלחו", "דואר יוצא",
                "elküldött", "elküldött elemek",
                "seolta", "r-phost seolta",
                "inviato", "posta inviata", "elementi inviati",
                "送信済み", "送信済メール", "送信メール",
                "보낸편지함", "발신함", "보낸메일",
                "nosūtītie", "nosūtītās vēstules",
                "išsiųsta", "išsiųsti laiškai",
                "mibgħuta", "posta mibgħuta",
                "wysłane", "elementy wysłane",
                "enviados", "itens enviados", "mensagens enviadas",
                "trimise", "elemente trimise", "mail trimis",
                "отправленные", "исходящие", "отправлено",
                "odoslané", "odoslaná pošta",
                "poslano", "poslana pošta",
                "enviado", "elementos enviados", "correo enviado",
                "skickat", "skickade objekt",
                "gönderilen", "gönderilmiş öğeler"
            };

            string folderNameLower = folder.Name.ToLowerInvariant();
            return sentFolderNames.Any(name => folderNameLower.Contains(name)) ||
                   folder.Attributes.HasFlag(FolderAttributes.Sent);
        }
    }
}
using MailKit;
using MailKit.Net.Imap;

namespace MailArchiver.Services.Providers.Imap
{
    /// <summary>
    /// IMAP folder discovery service with robust multi-strategy fallback for different IMAP server implementations.
    /// Supports namespace-based folder listing, recursive/non-recursive retrieval,
    /// non-subscribed folder discovery, and per-level traversal as last resort.
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
                    _logger.LogInformation("Using PersonalNamespace: {Path}", ns.Path ?? "(empty)");

                    // Strategy 1: Recursive LIST (fast path, works for most servers)
                    var recursiveSucceeded = false;
                    try
                    {
                        var rootFolders = await client.GetFoldersAsync(ns, StatusItems.None, subscribedOnly: false);
                        _logger.LogInformation("GetFoldersAsync(recursive) returned {Count} folders", rootFolders.Count);

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

                        recursiveSucceeded = true;
                    }
                    catch (Exception getFoldersEx)
                    {
                        // The recursive LIST command (LIST "" "<ns>*") failed. This happens with some
                        // IMAP servers (notably Outlook.com/MSA consumer accounts) when a folder
                        // name contains special characters such as parentheses, spaces, or commas
                        // and the server returns a malformed LIST response that MailKit cannot parse.
                        // The recursive call throws, discarding ALL folders in the response — even
                        // those parsed before the offending one. We must NOT re-issue the identical
                        // recursive call; instead we fall back to per-level traversal which queries
                        // each folder's children individually, isolating failures to single subtrees.
                        _logger.LogWarning(getFoldersEx,
                            "GetFoldersAsync(recursive) failed for {AccountName}: {Message}. " +
                            "This is common with Outlook.com when folder names contain special characters " +
                            "(parentheses, spaces, commas). Switching to per-level traversal fallback.",
                            accountName, getFoldersEx.Message);
                    }

                    // Strategy 2: Per-level traversal fallback (resilient against single bad folders)
                    if (!recursiveSucceeded)
                    {
                        await DiscoverFoldersPerLevelAsync(client, ns, accountName, allFolders);
                    }

                    // Strategy 3: If both strategies yielded very few folders, try alternative method
                    if (allFolders.Count <= 1)
                    {
                        _logger.LogInformation("Few folders found via GetFoldersAsync/per-level, trying alternative folder discovery method for {AccountName}", accountName);

                        try
                        {
                            var rootFolder = await client.GetFolderAsync(ns.Path ?? string.Empty);
                            _logger.LogInformation("Got root folder via alternative method: {FullName}", rootFolder.FullName);

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

                // Log the complete list of discovered folder names at Information level so users
                // can diagnose missing folders without enabling debug logging.
                if (allFolders.Count > 0)
                {
                    var folderNames = allFolders.Select(f => f.FullName).OrderBy(n => n).ToList();
                    _logger.LogInformation("All discovered folders for {AccountName}: {FolderNames}",
                        accountName, string.Join(", ", folderNames));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving folders for {AccountName}: {Message}", accountName, ex.Message);
            }

            return allFolders;
        }

        /// <summary>
        /// Discovers folders by traversing the hierarchy one level at a time using
        /// <see cref="IMailFolder.GetSubfoldersAsync"/>. Unlike the recursive <c>GetFoldersAsync</c>
        /// which issues a single <c>LIST "" "&lt;ns&gt;*"</c> command (and throws away ALL results
        /// if ANY single folder response is malformed), this method queries each parent folder's
        /// children individually. A failure while listing one folder's children is isolated to
        /// that subtree — sibling subtrees continue to be discovered.
        /// </summary>
        private async Task DiscoverFoldersPerLevelAsync(ImapClient client, FolderNamespace ns, string accountName, List<IMailFolder> allFolders)
        {
            _logger.LogInformation("Starting per-level folder traversal for account {AccountName}", accountName);

            var toProcess = new Queue<IMailFolder>();

            // Seed the BFS with top-level folders. We resolve the root folder by path,
            // then list its immediate children (single-level LIST "" "<root>/%").
            IMailFolder? rootFolder = null;
            try
            {
                rootFolder = await client.GetFolderAsync(ns.Path ?? string.Empty);
                _logger.LogInformation("Resolved root folder for per-level traversal: {FullName}", rootFolder.FullName);
            }
            catch (Exception rootEx)
            {
                _logger.LogWarning(rootEx, "Could not resolve root folder via GetFolderAsync('{Path}') for {AccountName}, " +
                    "falling back to client.GetFolders for top-level only", ns.Path ?? string.Empty, accountName);

                // Last resort: try a non-recursive top-level listing. Some servers handle this
                // even when the recursive wildcard fails.
                try
                {
                    var topFolders = await client.GetFoldersAsync(ns, StatusItems.None, subscribedOnly: false);
                    foreach (var topFolder in topFolders)
                    {
                        if (TryAddFolder(topFolder, allFolders))
                            toProcess.Enqueue(topFolder);
                    }
                    _logger.LogInformation("Fallback top-level listing returned {Count} folders", toProcess.Count);
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(fallbackEx, "Both per-level root resolution and top-level GetFoldersAsync failed for {AccountName}", accountName);
                }

                return;
            }

            // Also add the root itself if selectable (some servers expose it as a real folder)
            TryAddFolder(rootFolder, allFolders);

            // Enqueue root for BFS so its children are listed
            toProcess.Enqueue(rootFolder);

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

                        if (TryAddFolder(subFolder, allFolders))
                            toProcess.Enqueue(subFolder);
                    }
                }
                catch (Exception subEx)
                {
                    // A single bad folder's children cannot be listed — log and continue.
                    // Sibling folders at the same level are unaffected because each
                    // GetSubfoldersAsync is an independent IMAP command.
                    _logger.LogWarning(subEx, "Could not get subfolders for '{Folder}' — this subtree will be skipped " +
                        "(other folders are unaffected). Error: {Message}", currentFolder.FullName, subEx.Message);
                }
            }

            _logger.LogInformation("Per-level traversal complete for account {AccountName}, found {Count} folders",
                accountName, allFolders.Count);
        }

        /// <summary>
        /// Adds a folder to <paramref name="allFolders"/> if it is selectable and not already present.
        /// Returns true if the folder was added.
        /// </summary>
        private bool TryAddFolder(IMailFolder folder, List<IMailFolder> allFolders)
        {
            if (folder == null || string.IsNullOrEmpty(folder.FullName))
                return false;

            if (folder.Attributes.HasFlag(FolderAttributes.NonExistent) ||
                folder.Attributes.HasFlag(FolderAttributes.NoSelect))
            {
                _logger.LogInformation("Skipping non-selectable folder: {FullName} (Attributes: {Attributes})",
                    folder.FullName, folder.Attributes);
                return false;
            }

            if (allFolders.Any(f => f.FullName == folder.FullName))
                return false;

            allFolders.Add(folder);
            return true;
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
                    if (TryAddFolder(subfolder, allFolders))
                    {
                        await AddSubfoldersRecursivelySimple(subfolder, allFolders);
                    }
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
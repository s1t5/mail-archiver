using MailKit;
using MailKit.Net.Imap;

namespace MailArchiver.Services.Providers.Imap
{
    /// <summary>
    /// IMAP folder discovery service with robust multi-strategy folder listing.
    /// Runs a recursive LIST and a per-level traversal and merges the results into a
    /// union, so that neither a thrown nor a silently truncated server response can
    /// hide folders. Supports non-subscribed folder discovery on all servers.
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
            var discovered = new List<IMailFolder>();

            try
            {
                _logger.LogInformation("Retrieving all folders from IMAP server for account: {AccountName}", accountName);

                var recursiveFolders = new List<IMailFolder>();
                var perLevelFolders = new List<IMailFolder>();
                var lsubFolders = new List<IMailFolder>();

                if (client.PersonalNamespaces != null && client.PersonalNamespaces.Count > 0)
                {
                    var ns = client.PersonalNamespaces[0];
                    _logger.LogInformation("Using PersonalNamespace: {Path}", ns.Path ?? "(empty)");

                    // Strategy 1: Recursive LIST (fast path, works for most servers)
                    recursiveFolders = await DiscoverFoldersRecursiveAsync(client, ns, accountName);

                    // Strategy 2: Per-level traversal. This runs ALWAYS, not only as a fallback,
                    // because the recursive LIST on Outlook.com (personal/MSA) accounts can fail
                    // in two distinct ways when folder names contain special characters such as
                    // parentheses, spaces or commas (e.g. "NYC (work, June)"):
                    //
                    //   (a) the server returns a malformed LIST line; MailKit throws an
                    //       ImapProtocolException and ALL results of the recursive call are
                    //       discarded, or
                    //   (b) the server returns a *syntactically valid but truncated* LIST
                    //       response that silently stops at the offending folder — no exception,
                    //       no error, just missing folders.
                    //
                    // Per-level traversal queries each folder's children individually, so a bad
                    // folder only breaks its own (or its parent's) LIST command — sibling subtrees
                    // and everything before/after the offending entry are still discovered.
                    perLevelFolders = await DiscoverFoldersPerLevelAsync(client, ns, accountName);

                    // Strategy 3: LSUB (subscribed folders). Some servers (notably Outlook.com)
                    // omit folders from LIST responses that they still report via LSUB — e.g.
                    // folders whose PR_CONTAINER_CLASS was set to a non-IPF.Note value by
                    // third-party clients using EWS. One extra command, merged into the union.
                    lsubFolders = await DiscoverSubscribedFoldersAsync(client, ns, accountName);
                }
                else
                {
                    _logger.LogWarning("No PersonalNamespaces available for account {AccountName}", accountName);
                }

                var folders = MergeFolderLists(
                    MergeFolderLists(recursiveFolders, perLevelFolders, f => f.FullName),
                    lsubFolders, f => f.FullName);

                // Diagnostic: if the per-level traversal or LSUB found folders that the recursive
                // LIST did not return, the server most likely returned a truncated/malformed LIST
                // response or filters folders out of LIST entirely. Surface this loudly so users
                // can see WHY folders were missing.
                if ((perLevelFolders.Count > 0 || lsubFolders.Count > 0) && folders.Count > recursiveFolders.Count)
                {
                    var recovered = folders
                        .Where(f => !recursiveFolders.Any(r => r.FullName == f.FullName))
                        .Select(f => f.FullName)
                        .ToList();

                    if (recovered.Count > 0)
                    {
                        const int maxLoggedNames = 20;
                        var loggedNames = string.Join(", ", recovered.Take(maxLoggedNames));
                        var suffix = recovered.Count > maxLoggedNames ? $", ... and {recovered.Count - maxLoggedNames} more" : "";

                        _logger.LogWarning(
                            "The recursive IMAP LIST for account {AccountName} missed {RecoveredCount} folder(s) " +
                            "(recursive found: {RecursiveCount}). This happens on Outlook.com when folder names " +
                            "contain special characters (parentheses, spaces, commas) causing truncated or malformed " +
                            "LIST responses, or when folders were created through third-party clients (EWS) with a " +
                            "non-IPF.Note PR_CONTAINER_CLASS and are filtered out of LIST entirely. " +
                            "Recovered via per-level traversal/LSUB: {RecoveredFolders}{Suffix}",
                            accountName, recovered.Count, recursiveFolders.Count, loggedNames, suffix);
                    }
                }

                // IMPORTANT: Always add INBOX explicitly first (some servers hide or rename it
                // in listings). It is merged in front of the discovered folders so it keeps
                // priority in the sync order; a server-discovered duplicate is dropped.
                try
                {
                    var inbox = client.Inbox;
                    if (inbox != null &&
                        !inbox.Attributes.HasFlag(FolderAttributes.NonExistent) &&
                        !inbox.Attributes.HasFlag(FolderAttributes.NoSelect))
                    {
                        _logger.LogInformation("Adding INBOX explicitly: {FullName}", inbox.FullName);
                        discovered = MergeFolderLists(new List<IMailFolder> { inbox }, folders, f => f.FullName);
                    }
                    else
                    {
                        discovered = folders;
                    }
                }
                catch (Exception inboxEx)
                {
                    _logger.LogWarning(inboxEx, "Could not access INBOX for {AccountName}", accountName);
                    discovered = folders;
                }

                _logger.LogInformation("Total selectable folders found for {AccountName}: {Count}", accountName, discovered.Count);

                // Log the complete list of discovered folder names at Information level so users
                // can diagnose missing folders without enabling debug logging.
                if (discovered.Count > 0)
                {
                    var folderNames = discovered.Select(f => f.FullName).OrderBy(n => n).ToList();
                    _logger.LogInformation("All discovered folders for {AccountName}: {FolderNames}",
                        accountName, string.Join(", ", folderNames));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving folders for {AccountName}: {Message}", accountName, ex.Message);
            }

            return discovered;
        }

        /// <summary>
        /// Strategy 1: recursive <c>LIST "" "&lt;ns&gt;*"</c> — a single command covering the whole tree.
        /// Returns only selectable folders; returns an empty list if the command or its response
        /// parsing fails (Provider returns malformed responses for folders with special
        /// characters in their names, which makes MailKit throw and discard all results).
        /// </summary>
        private async Task<List<IMailFolder>> DiscoverFoldersRecursiveAsync(ImapClient client, FolderNamespace ns, string accountName)
        {
            var folders = new List<IMailFolder>();

            try
            {
                var rootFolders = await client.GetFoldersAsync(ns, StatusItems.None, subscribedOnly: false);
                _logger.LogInformation("GetFoldersAsync(recursive) returned {Count} folders", rootFolders.Count);

                foreach (var folder in rootFolders)
                {
                    _logger.LogDebug("Found folder: Name={Name}, FullName={FullName}, Attributes={Attributes}",
                        folder.Name ?? "NULL", folder.FullName ?? "NULL", folder.Attributes);

                    if (IsSelectable(folder) && !folders.Any(f => f.FullName == folder.FullName))
                    {
                        folders.Add(folder);
                    }
                }
            }
            catch (Exception getFoldersEx)
            {
                // Any results of the recursive call have been discarded by MailKit. The
                // per-level traversal that runs after this method will still discover the tree.
                _logger.LogWarning(getFoldersEx,
                    "GetFoldersAsync(recursive) failed for {AccountName}: {Message}. " +
                    "This is common with Provider when folder names contain special characters " +
                    "(parentheses, spaces, commas). The per-level traversal will fill the gap.",
                    accountName, getFoldersEx.Message);
            }

            return folders;
        }

        /// <summary>
        /// Strategy 2: discovers folders by traversing the hierarchy one level at a time using
        /// <see cref="IMailFolder.GetSubfoldersAsync"/>. Unlike the recursive <c>GetFoldersAsync</c>
        /// which issues a single <c>LIST "" "&lt;ns&gt;*"</c> command (and throws away ALL results
        /// if ANY single folder response is malformed, or may silently return a truncated list),
        /// this method queries each parent folder's children individually. A failure while listing
        /// one folder's children is isolated to that subtree — sibling subtrees continue to be
        /// discovered. Returns only selectable folders.
        /// </summary>
        private async Task<List<IMailFolder>> DiscoverFoldersPerLevelAsync(ImapClient client, FolderNamespace ns, string accountName)
        {
            var folders = new List<IMailFolder>();

            try
            {
                _logger.LogInformation("Starting per-level folder traversal for account {AccountName}", accountName);

                var toProcess = new Queue<IMailFolder>();
                var seen = new HashSet<string>(StringComparer.Ordinal);

                // Seed the BFS with the namespace root folder, resolved by path.
                IMailFolder? rootFolder = null;
                try
                {
                    rootFolder = await client.GetFolderAsync(ns.Path ?? string.Empty);
                    _logger.LogInformation("Resolved root folder for per-level traversal: {FullName}", rootFolder?.FullName ?? "(empty)");
                }
                catch (Exception rootEx)
                {
                    _logger.LogWarning(rootEx, "Could not resolve root folder via GetFolderAsync('{Path}') for {AccountName}",
                        ns.Path ?? string.Empty, accountName);
                }

                if (rootFolder != null)
                {
                    if (!string.IsNullOrEmpty(rootFolder.FullName))
                        seen.Add(rootFolder.FullName);

                    if (IsSelectable(rootFolder))
                        folders.Add(rootFolder);

                    // Enqueue root for BFS so its children are listed (even if the root
                    // itself is not selectable).
                    toProcess.Enqueue(rootFolder);
                }
                else
                {
                    // Last-resort seed: a recursive listing. If it fails too, there is
                    // nothing to traverse from.
                    _logger.LogWarning("No root folder could be resolved for per-level traversal of {AccountName}, " +
                        "falling back to GetFoldersAsync as BFS seed", accountName);

                    try
                    {
                        var topFolders = await client.GetFoldersAsync(ns, StatusItems.None, subscribedOnly: false);
                        foreach (var topFolder in topFolders)
                        {
                            if (topFolder == null || string.IsNullOrEmpty(topFolder.FullName) || !seen.Add(topFolder.FullName))
                                continue;

                            if (IsSelectable(topFolder))
                                folders.Add(topFolder);

                            toProcess.Enqueue(topFolder);
                        }
                        _logger.LogInformation("Fallback top-level listing returned {Count} folders", toProcess.Count);
                    }
                    catch (Exception fallbackEx)
                    {
                        _logger.LogError(fallbackEx, "Both per-level root resolution and fallback GetFoldersAsync failed for {AccountName}", accountName);
                        return folders;
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
                            if (subFolder == null || string.IsNullOrEmpty(subFolder.FullName))
                                continue;

                            if (!seen.Add(subFolder.FullName))
                                continue;

                            _logger.LogDebug("Found subfolder: Name={Name}, FullName={FullName}, Attributes={Attributes}",
                                subFolder.Name ?? "NULL", subFolder.FullName ?? "NULL", subFolder.Attributes);

                            if (IsSelectable(subFolder))
                            {
                                folders.Add(subFolder);
                            }
                            else
                            {
                                _logger.LogInformation("Skipping non-selectable folder: {FullName} (Attributes: {Attributes})",
                                    subFolder.FullName, subFolder.Attributes);
                            }

                            // Enqueue non-selectable containers too — their children may still
                            // be selectable (common on servers using \Noselect parent folders).
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

                _logger.LogInformation("Per-level traversal complete for account {AccountName}, found {Count} selectable folders",
                    accountName, folders.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Per-level folder traversal failed for {AccountName}: {Message}", accountName, ex.Message);
            }

            return folders;
        }

        /// <summary>
        /// Strategy 3: LSUB — lists subscribed folders. Some servers (notably Outlook.com) omit
        /// folders from LIST responses that they still report via LSUB, e.g. folders created
        /// through third-party clients via EWS whose PR_CONTAINER_CLASS is not IPF.Note.
        /// Returns only selectable folders; returns an empty list if the command fails.
        /// </summary>
        private async Task<List<IMailFolder>> DiscoverSubscribedFoldersAsync(ImapClient client, FolderNamespace ns, string accountName)
        {
            var folders = new List<IMailFolder>();

            try
            {
                var subscribedFolders = await client.GetFoldersAsync(ns, StatusItems.None, subscribedOnly: true);
                _logger.LogInformation("GetFoldersAsync(LSUB) returned {Count} subscribed folders", subscribedFolders.Count);

                foreach (var folder in subscribedFolders)
                {
                    _logger.LogDebug("Found subscribed folder: Name={Name}, FullName={FullName}, Attributes={Attributes}",
                        folder.Name ?? "NULL", folder.FullName ?? "NULL", folder.Attributes);

                    if (IsSelectable(folder) && !folders.Any(f => f.FullName == folder.FullName))
                    {
                        folders.Add(folder);
                    }
                }
            }
            catch (Exception lsubEx)
            {
                // LSUB is best-effort: some servers do not support it or report errors. The
                // LIST-based strategies remain the authoritative discovery sources.
                _logger.LogWarning(lsubEx,
                    "GetFoldersAsync(LSUB) failed for {AccountName}: {Message}. Continuing with LIST-based discovery results.",
                    accountName, lsubEx.Message);
            }

            return folders;
        }

        /// <summary>
        /// Merges two folder lists into a union, deduplicating by the path returned from
        /// <paramref name="fullPathSelector"/>. Entries from <paramref name="primary"/> are kept
        /// first and take precedence over <paramref name="secondary"/> entries with the same path.
        /// </summary>
        internal static List<T> MergeFolderLists<T>(List<T> primary, List<T> secondary, Func<T, string?> fullPathSelector) where T : class
        {
            var result = new List<T>(primary.Count + secondary.Count);
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var source in new[] { primary, secondary })
            {
                foreach (var item in source)
                {
                    if (item == null)
                        continue;

                    var path = fullPathSelector(item);
                    if (string.IsNullOrEmpty(path) || !seen.Add(path))
                        continue;

                    result.Add(item);
                }
            }

            return result;
        }

        /// <summary>
        /// Returns true when a folder exists and can be selected/opened.
        /// </summary>
        private static bool IsSelectable(IMailFolder folder)
        {
            return folder != null &&
                   !string.IsNullOrEmpty(folder.FullName) &&
                   !folder.Attributes.HasFlag(FolderAttributes.NonExistent) &&
                   !folder.Attributes.HasFlag(FolderAttributes.NoSelect);
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

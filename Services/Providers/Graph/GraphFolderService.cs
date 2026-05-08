using MailArchiver.Models;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace MailArchiver.Services.Providers.Graph
{
    /// <summary>
    /// Service for Microsoft Graph folder operations: listing, path building, and folder hierarchy resolution/creation.
    /// </summary>
    public class GraphFolderService : IGraphFolderService
    {
        private readonly GraphAuthClientFactory _authFactory;
        private readonly ILogger<GraphFolderService> _logger;

        public GraphFolderService(GraphAuthClientFactory authFactory, ILogger<GraphFolderService> logger)
        {
            _authFactory = authFactory;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<List<string>> GetMailFoldersAsync(MailAccount account)
        {
            try
            {
                var graphClient = _authFactory.CreateGraphClient(account);
                var folders = await GetAllMailFoldersAsync(graphClient, account.EmailAddress);
                var folderPaths = BuildFolderPathDictionary(folders);
                return folderPaths.Values.OrderBy(f => f).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving Graph API folders for account {AccountId}: {Message}", account.Id, ex.Message);
                return new List<string>();
            }
        }

        /// <inheritdoc/>
        public async Task<List<MailFolder>> GetAllMailFoldersAsync(GraphServiceClient graphClient, string userPrincipalName)
        {
            var folders = new List<MailFolder>();

            try
            {
                _logger.LogInformation("Starting to retrieve all mail folders for user: {UserPrincipalName}", userPrincipalName);

                var response = await graphClient.Users[userPrincipalName].MailFolders.GetAsync((requestConfiguration) =>
                {
                    requestConfiguration.QueryParameters.Select = new string[] { "id", "displayName", "parentFolderId", "childFolderCount", "totalItemCount" };
                    requestConfiguration.QueryParameters.Top = 250;
                    requestConfiguration.QueryParameters.IncludeHiddenFolders = "true";
                });

                int folderCount = 0;
                int pageCount = 0;

                while (response?.Value != null)
                {
                    pageCount++;
                    var currentPageCount = response.Value.Count;
                    folderCount += currentPageCount;

                    _logger.LogInformation("Processing folder page {PageNumber} with {FolderCount} folders (Total so far: {TotalFolders})",
                        pageCount, currentPageCount, folderCount);

                    folders.AddRange(response.Value);

                    foreach (var folder in response.Value)
                    {
                        _logger.LogDebug("Found folder: '{FolderName}' (ID: {FolderId}, ChildCount: {ChildCount}, ItemCount: {ItemCount})",
                            folder.DisplayName, folder.Id, folder.ChildFolderCount, folder.TotalItemCount);
                    }

                    if (!string.IsNullOrEmpty(response.OdataNextLink))
                    {
                        _logger.LogInformation("Fetching next page of folders...");
                        response = await graphClient.Users[userPrincipalName].MailFolders.WithUrl(response.OdataNextLink).GetAsync();
                    }
                    else
                    {
                        break;
                    }
                }

                _logger.LogInformation("Retrieved {TotalFolders} top-level folders across {PageCount} pages", folderCount, pageCount);

                var topLevelFolders = folders.ToList();
                foreach (var folder in topLevelFolders)
                {
                    if (folder.ChildFolderCount > 0)
                    {
                        _logger.LogDebug("Getting child folders for: '{FolderName}' (Expected children: {ChildCount})",
                            folder.DisplayName, folder.ChildFolderCount);

                        var childFolders = await GetChildFoldersAsync(graphClient, userPrincipalName, folder.Id);
                        folders.AddRange(childFolders);

                        _logger.LogDebug("Added {ChildFolderCount} child folders for '{FolderName}'",
                            childFolders.Count, folder.DisplayName);
                    }
                }

                _logger.LogInformation("Total folders retrieved (including children): {TotalFolders}", folders.Count);

                var folderNames = folders.Select(f => f.DisplayName).OrderBy(name => name).ToList();
                _logger.LogInformation("All folders found: {FolderNames}", string.Join(", ", folderNames));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting mail folders from Graph API: {Message}", ex.Message);
                throw;
            }

            return folders;
        }

        /// <summary>
        /// Recursively retrieves child folders for a given parent folder.
        /// </summary>
        public async Task<List<MailFolder>> GetChildFoldersAsync(GraphServiceClient graphClient, string userPrincipalName, string parentFolderId)
        {
            var childFolders = new List<MailFolder>();

            try
            {
                _logger.LogDebug("Starting to retrieve child folders for parent: {ParentFolderId}", parentFolderId);

                var response = await graphClient.Users[userPrincipalName].MailFolders[parentFolderId].ChildFolders.GetAsync((requestConfiguration) =>
                {
                    requestConfiguration.QueryParameters.Select = new string[] { "id", "displayName", "parentFolderId", "childFolderCount", "totalItemCount" };
                    requestConfiguration.QueryParameters.Top = 250;
                    requestConfiguration.QueryParameters.IncludeHiddenFolders = "true";
                });

                int childFolderCount = 0;
                int pageCount = 0;

                while (response?.Value != null)
                {
                    pageCount++;
                    var currentPageCount = response.Value.Count;
                    childFolderCount += currentPageCount;

                    _logger.LogDebug("Processing child folder page {PageNumber} with {FolderCount} folders for parent {ParentFolderId} (Total so far: {TotalFolders})",
                        pageCount, currentPageCount, parentFolderId, childFolderCount);

                    childFolders.AddRange(response.Value);

                    foreach (var folder in response.Value)
                    {
                        _logger.LogDebug("Found child folder: '{FolderName}' (ID: {FolderId}, Parent: {ParentFolderId}, ChildCount: {ChildCount}, ItemCount: {ItemCount})",
                            folder.DisplayName, folder.Id, folder.ParentFolderId, folder.ChildFolderCount, folder.TotalItemCount);
                    }

                    if (!string.IsNullOrEmpty(response.OdataNextLink))
                    {
                        _logger.LogDebug("Fetching next page of child folders for parent {ParentFolderId}...", parentFolderId);
                        response = await graphClient.Users[userPrincipalName].MailFolders[parentFolderId].ChildFolders.WithUrl(response.OdataNextLink).GetAsync();
                    }
                    else
                    {
                        break;
                    }
                }

                _logger.LogDebug("Retrieved {TotalChildFolders} child folders across {PageCount} pages for parent {ParentFolderId}",
                    childFolderCount, pageCount, parentFolderId);

                var currentChildFolders = childFolders.ToList();
                foreach (var folder in currentChildFolders)
                {
                    if (folder.ChildFolderCount > 0)
                    {
                        _logger.LogDebug("Getting grandchild folders for: '{FolderName}' (Expected children: {ChildCount})",
                            folder.DisplayName, folder.ChildFolderCount);

                        var grandChildFolders = await GetChildFoldersAsync(graphClient, userPrincipalName, folder.Id);
                        childFolders.AddRange(grandChildFolders);

                        _logger.LogDebug("Added {GrandChildFolderCount} grandchild folders for '{FolderName}'",
                            grandChildFolders.Count, folder.DisplayName);
                    }
                }

                _logger.LogDebug("Total child and grandchild folders retrieved for parent {ParentFolderId}: {TotalFolders}",
                    parentFolderId, childFolders.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting child folders for parent {ParentFolderId}: {Message}", parentFolderId, ex.Message);
            }

            return childFolders;
        }

        /// <inheritdoc/>
        public Dictionary<string, string> BuildFolderPathDictionary(List<MailFolder> folders)
        {
            var folderPaths = new Dictionary<string, string>();

            var folderDict = folders
                .Where(f => !string.IsNullOrEmpty(f.Id))
                .GroupBy(f => f.Id!)
                .ToDictionary(g => g.Key, g => g.First());

            foreach (var folder in folders)
            {
                if (string.IsNullOrEmpty(folder.Id))
                    continue;

                var path = BuildFolderPathRecursive(folder, folderDict, new HashSet<string>());
                if (!string.IsNullOrEmpty(path))
                {
                    folderPaths[folder.Id] = path;
                }
            }

            return folderPaths;
        }

        /// <summary>
        /// Recursively builds the full path for a folder by traversing parent references.
        /// Includes cycle detection to prevent infinite loops.
        /// </summary>
        private string BuildFolderPathRecursive(MailFolder folder, Dictionary<string, MailFolder> folderDict, HashSet<string> visited)
        {
            if (folder == null || string.IsNullOrEmpty(folder.Id))
                return string.Empty;

            if (visited.Contains(folder.Id))
            {
                _logger.LogWarning("Circular folder reference detected for folder {FolderId}", folder.Id);
                return folder.DisplayName ?? "Unknown";
            }
            visited.Add(folder.Id);

            var displayName = folder.DisplayName ?? "Unknown";

            if (string.IsNullOrEmpty(folder.ParentFolderId))
                return displayName;

            if (folderDict.TryGetValue(folder.ParentFolderId, out var parentFolder))
            {
                var parentPath = BuildFolderPathRecursive(parentFolder, folderDict, visited);
                if (!string.IsNullOrEmpty(parentPath))
                {
                    return $"{parentPath}/{displayName}";
                }
            }

            return displayName;
        }

        /// <inheritdoc/>
        public async Task<MailFolder?> EnsureFolderPathAsync(
            GraphServiceClient graphClient,
            string userPrincipalName,
            string folderPath,
            List<MailFolder> existingFolders,
            bool createIfMissing)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
                return null;

            var parts = folderPath
                .Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();

            if (parts.Count == 0)
                return null;

            var folderDict = existingFolders
                .Where(f => !string.IsNullOrEmpty(f.Id))
                .GroupBy(f => f.Id!)
                .ToDictionary(g => g.Key, g => g.First());

            bool IsTopLevel(MailFolder f) =>
                string.IsNullOrEmpty(f.ParentFolderId) || !folderDict.ContainsKey(f.ParentFolderId!);

            MailFolder? current = existingFolders.FirstOrDefault(f =>
                IsTopLevel(f) &&
                !string.IsNullOrEmpty(f.DisplayName) &&
                f.DisplayName.Equals(parts[0], StringComparison.OrdinalIgnoreCase));

            if (current == null)
            {
                if (!createIfMissing)
                {
                    _logger.LogDebug("Top-level folder '{Name}' not found and creation disabled", parts[0]);
                    return null;
                }

                try
                {
                    var newFolder = new MailFolder
                    {
                        DisplayName = parts[0],
                        IsHidden = false
                    };

                    current = await graphClient.Users[userPrincipalName].MailFolders.PostAsync(newFolder);
                    if (current != null)
                    {
                        existingFolders.Add(current);
                        if (!string.IsNullOrEmpty(current.Id))
                        {
                            folderDict[current.Id!] = current;
                        }
                        _logger.LogInformation("Created top-level mail folder '{Name}' for {Upn}", parts[0], userPrincipalName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create top-level mail folder '{Name}' for {Upn}", parts[0], userPrincipalName);
                    return null;
                }
            }

            if (current == null)
                return null;

            for (int i = 1; i < parts.Count; i++)
            {
                var part = parts[i];
                var parentId = current.Id;

                MailFolder? sub = existingFolders.FirstOrDefault(f =>
                    !string.IsNullOrEmpty(f.DisplayName) &&
                    f.ParentFolderId == parentId &&
                    f.DisplayName.Equals(part, StringComparison.OrdinalIgnoreCase));

                if (sub == null)
                {
                    if (!createIfMissing)
                    {
                        _logger.LogDebug("Subfolder '{Part}' under '{Parent}' not found and creation disabled",
                            part, current.DisplayName);
                        return null;
                    }

                    try
                    {
                        var newFolder = new MailFolder
                        {
                            DisplayName = part,
                            IsHidden = false
                        };

                        sub = await graphClient.Users[userPrincipalName]
                            .MailFolders[parentId]
                            .ChildFolders
                            .PostAsync(newFolder);

                        if (sub != null)
                        {
                            existingFolders.Add(sub);
                            if (!string.IsNullOrEmpty(sub.Id))
                            {
                                folderDict[sub.Id!] = sub;
                            }
                            _logger.LogInformation("Created subfolder '{Part}' under '{Parent}'", part, current.DisplayName);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to create subfolder '{Part}' under '{Parent}'", part, current.DisplayName);
                        return null;
                    }
                }

                if (sub == null)
                    return null;

                current = sub;
            }

            return current;
        }

        /// <inheritdoc/>
        public async Task<MailFolder?> GetWellKnownInboxAsync(
            GraphServiceClient graphClient,
            string userPrincipalName,
            List<MailFolder> existingFolders)
        {
            try
            {
                var inbox = await graphClient.Users[userPrincipalName].MailFolders["inbox"].GetAsync();
                if (inbox != null)
                {
                    if (!string.IsNullOrEmpty(inbox.Id) && !existingFolders.Any(f => f.Id == inbox.Id))
                    {
                        existingFolders.Add(inbox);
                    }
                    return inbox;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resolve well-known Inbox for account {Upn}", userPrincipalName);
            }

            return existingFolders.FirstOrDefault(f =>
                !string.IsNullOrEmpty(f.DisplayName) &&
                f.DisplayName.Equals("Inbox", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Determines if a folder is an outgoing (sent) folder based on its attributes.
        /// </summary>
        public bool IsOutgoingFolder(MailFolder folder)
        {
            var sentFolderNames = new[]
            {
                "sent", "sent items", "sent mail", "outbox",
                "gesendet", "gesendete objekte", "postausgang",
                "envoyé", "éléments envoyés", "boîte d'envoi",
                "inviato", "posta inviata", "posta in uscita"
            };

            string folderNameLower = folder.DisplayName?.ToLowerInvariant() ?? "";
            return sentFolderNames.Any(name => folderNameLower.Contains(name));
        }

        /// <summary>
        /// Checks if a folder name indicates a drafts folder.
        /// </summary>
        public bool IsDraftsFolder(string? folderName)
        {
            var draftsFolderNames = new[]
            {
                "drafts", "entwürfe", "brouillons", "bozze"
            };

            string folderNameLower = folderName?.ToLowerInvariant() ?? "";
            return draftsFolderNames.Any(name => folderNameLower.Contains(name));
        }

        /// <summary>
        /// Checks if a folder name indicates outgoing mail based on its name in multiple languages.
        /// </summary>
        public bool IsOutgoingFolderByName(string? folderName)
        {
            var outgoingFolderNames = new[]
            {
                "outgoing", "sent", "sent items", "sent mail", "outbox",
                "gesendet", "gesendete objekte", "gesendete nachrichten", "postausgang",
                "envoyé", "éléments envoyés", "boîte d'envoi", "messages envoyés",
                "enviados", "elementos enviados", "correo enviado", "bandeja de salida",
                "inviato", "posta inviata", "elementi inviati", "posta in uscita",
                "verzonden", "verzonden items", "verzonden e-mail", "postvak uit",
                "исходящие", "отправленные", "исходящая почта",
                "已发送", "发件箱", "已传送",
                "送信済み", "送信済メール", "送信メール", "送信トレイ",
                "enviados", "itens enviados", "mensagens enviadas", "caixa de saída",
                "الصادر", "المرسلة", "بريد الصادر",
                "out", "send"
            };

            string folderNameLower = folderName?.ToLowerInvariant() ?? "";
            return outgoingFolderNames.Any(name => folderNameLower.Contains(name));
        }
    }
}
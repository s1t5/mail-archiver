using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Models.ViewModels;
using MailArchiver.Utilities;
using MailArchiver.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MimeKit;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MailArchiver.Services.Core
{
    /// <summary>
    /// Core email service providing provider-independent functionality
    /// Handles search, export, archiving, and dashboard operations
    /// </summary>
    public class EmailCoreService
    {
        private readonly MailArchiverDbContext _context;
        private readonly ILogger<EmailCoreService> _logger;
        private readonly DateTimeHelper _dateTimeHelper;
        private readonly BatchOperationOptions _batchOptions;

        // Constants for HTML truncation
        private static readonly string TruncationNotice = @"
                    <div style='background-color: #f8f9fa; border: 1px solid #dee2e6; border-radius: 5px; padding: 15px; margin: 10px 0; font-family: Arial, sans-serif;'>
                        <h4 style='color: #495057; margin-top: 0;'>📎 Email content has been truncated</h4>
                        <p style='color: #6c757d; margin-bottom: 10px;'>
                            This email contains very large HTML content (over 1 MB) that has been truncated for better performance.
                        </p>
                        <p style='color: #6c757d; margin-bottom: 0;'>
                            <strong>The complete original HTML content has been saved as an attachment.</strong><br>
                            Look for a file named 'original_content_*.html' in the attachments.
                        </p>
                    </div>";

        private static readonly int TruncationOverhead = Encoding.UTF8.GetByteCount(TruncationNotice + "</body></html>");
        private const int MaxHtmlSizeBytes = 1_000_000;

        public EmailCoreService(
            MailArchiverDbContext context,
            ILogger<EmailCoreService> logger,
            DateTimeHelper dateTimeHelper,
            IOptions<BatchOperationOptions> batchOptions)
        {
            _context = context;
            _logger = logger;
            _dateTimeHelper = dateTimeHelper;
            _batchOptions = batchOptions.Value;
        }

        #region Search Methods

        public async Task<(List<ArchivedEmail> Emails, int TotalCount)> SearchEmailsAsync(
            string searchTerm,
            DateTime? fromDate,
            DateTime? toDate,
            int? accountId,
            string folderName,
            bool? isOutgoing,
            int skip,
            int take,
            List<int> allowedAccountIds = null,
            string sortBy = "SentDate",
            string sortOrder = "desc")
        {
            var startTime = DateTime.UtcNow;

            // Validate pagination parameters to prevent excessive data loading
            if (take > 1000) take = 1000;
            if (skip < 0) skip = 0;

            try
            {
                return await SearchEmailsOptimizedAsync(searchTerm, fromDate, toDate, accountId, folderName, isOutgoing, skip, take, allowedAccountIds, sortBy, sortOrder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Optimized search failed, falling back to Entity Framework search");
                return await SearchEmailsEFAsync(searchTerm, fromDate, toDate, accountId, folderName, isOutgoing, skip, take, allowedAccountIds);
            }
        }

        private async Task<(List<ArchivedEmail> Emails, int TotalCount)> SearchEmailsOptimizedAsync(
            string searchTerm,
            DateTime? fromDate,
            DateTime? toDate,
            int? accountId,
            string folderName,
            bool? isOutgoing,
            int skip,
            int take,
            List<int> allowedAccountIds = null,
            string sortBy = "SentDate",
            string sortOrder = "desc")
        {
            var startTime = DateTime.UtcNow;
            var whereConditions = new List<string>();
            var parameters = new List<Npgsql.NpgsqlParameter>();
            var paramCounter = 0;

            // Full-text search condition
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var (tsQuery, phrases, fieldSearches, fieldPhrases) = ParseSearchTermForTsQuery(searchTerm);
                var searchConditions = new List<string>();

                if (!string.IsNullOrEmpty(tsQuery))
                {
                    searchConditions.Add($@"
                        to_tsvector('simple', 
                            COALESCE(""Subject"", '') || ' ' || 
                            COALESCE(""Body"", '') || ' ' || 
                            COALESCE(""From"", '') || ' ' || 
                            COALESCE(""To"", '') || ' ' || 
                            COALESCE(""Cc"", '') || ' ' || 
                            COALESCE(""Bcc"", '')) 
                        @@ to_tsquery('simple', @param{paramCounter})");
                    parameters.Add(new Npgsql.NpgsqlParameter($"@param{paramCounter}", tsQuery));
                    paramCounter++;
                }

                foreach (var phrase in phrases)
                {
                    searchConditions.Add($@"(
                        POSITION(LOWER(@param{paramCounter}) IN LOWER(COALESCE(""Subject"", ''))) > 0 OR
                        POSITION(LOWER(@param{paramCounter}) IN LOWER(COALESCE(""Body"", ''))) > 0 OR
                        POSITION(LOWER(@param{paramCounter}) IN LOWER(COALESCE(""From"", ''))) > 0 OR
                        POSITION(LOWER(@param{paramCounter}) IN LOWER(COALESCE(""To"", ''))) > 0 OR
                        POSITION(LOWER(@param{paramCounter}) IN LOWER(COALESCE(""Cc"", ''))) > 0 OR
                        POSITION(LOWER(@param{paramCounter}) IN LOWER(COALESCE(""Bcc"", ''))) > 0
                    )");
                    parameters.Add(new Npgsql.NpgsqlParameter($"@param{paramCounter}", phrase));
                    paramCounter++;
                }

                foreach (var fieldSearch in fieldSearches)
                {
                    var field = fieldSearch.Key;
                    var terms = fieldSearch.Value;
                    var columnName = GetColumnNameForField(field);

                    if (!string.IsNullOrEmpty(columnName))
                    {
                        foreach (var term in terms)
                        {
                            searchConditions.Add($@"
                                POSITION(LOWER(@param{paramCounter}) IN LOWER(COALESCE(""{columnName}"", ''))) > 0");
                            parameters.Add(new Npgsql.NpgsqlParameter($"@param{paramCounter}", term));
                            paramCounter++;
                        }
                    }
                }

                foreach (var fieldPhrase in fieldPhrases)
                {
                    var field = fieldPhrase.Key;
                    var currentFieldPhrases = fieldPhrase.Value;
                    var columnName = GetColumnNameForField(field);

                    if (!string.IsNullOrEmpty(columnName))
                    {
                        foreach (var phrase in currentFieldPhrases)
                        {
                            searchConditions.Add($@"
                                POSITION(LOWER(@param{paramCounter}) IN LOWER(COALESCE(""{columnName}"", ''))) > 0");
                            parameters.Add(new Npgsql.NpgsqlParameter($"@param{paramCounter}", phrase));
                            paramCounter++;
                        }
                    }
                }

                if (searchConditions.Any())
                {
                    whereConditions.Add($"({string.Join(" AND ", searchConditions)})");
                }
            }

            // Account filtering
            if (accountId.HasValue)
            {
                if (allowedAccountIds != null && allowedAccountIds.Any() && !allowedAccountIds.Contains(accountId.Value))
                {
                    _logger.LogWarning("User attempted to access account {AccountId} which is not in their allowed accounts list", accountId.Value);
                    return (new List<ArchivedEmail>(), 0);
                }

                whereConditions.Add($@"""MailAccountId"" = @param{paramCounter}");
                parameters.Add(new Npgsql.NpgsqlParameter($"@param{paramCounter}", accountId.Value));
                paramCounter++;
            }
            else if (allowedAccountIds != null)
            {
                if (allowedAccountIds.Any())
                {
                    whereConditions.Add($@"""MailAccountId"" = ANY(@param{paramCounter})");
                    parameters.Add(new Npgsql.NpgsqlParameter($"@param{paramCounter}", allowedAccountIds.ToArray()));
                    paramCounter++;
                }
                else
                {
                    _logger.LogWarning("User has no allowed accounts, returning empty result set");
                    return (new List<ArchivedEmail>(), 0);
                }
            }

            // Date filtering
            if (fromDate.HasValue)
            {
                whereConditions.Add($@"""SentDate"" >= @param{paramCounter}");
                parameters.Add(new Npgsql.NpgsqlParameter($"@param{paramCounter}", fromDate.Value));
                paramCounter++;
            }

            if (toDate.HasValue)
            {
                var endDate = toDate.Value.AddDays(1).AddSeconds(-1);
                whereConditions.Add($@"""SentDate"" <= @param{paramCounter}");
                parameters.Add(new Npgsql.NpgsqlParameter($"@param{paramCounter}", endDate));
                paramCounter++;
            }

            if (isOutgoing.HasValue)
            {
                whereConditions.Add($@"""IsOutgoing"" = @param{paramCounter}");
                parameters.Add(new Npgsql.NpgsqlParameter($"@param{paramCounter}", isOutgoing.Value));
                paramCounter++;
            }

            // Folder filtering
            if (!string.IsNullOrEmpty(folderName))
            {
                whereConditions.Add($@"""FolderName"" = @param{paramCounter}");
                parameters.Add(new Npgsql.NpgsqlParameter($"@param{paramCounter}", folderName));
                paramCounter++;
            }

            var whereClause = whereConditions.Any() ? "WHERE " + string.Join(" AND ", whereConditions) : "";

            // Count query
            var countSql = $@"
                SELECT COUNT(*)
                FROM mail_archiver.""ArchivedEmails""
                {whereClause}";

            var totalCount = await ExecuteScalarQueryAsync<int>(countSql, CloneParameters(parameters));

            // Build ORDER BY clause
            var orderByClause = GetOrderByClause(sortBy, sortOrder);

            // Data query
            var dataSql = $@"
                SELECT e.""Id"", e.""MailAccountId"", e.""MessageId"", e.""Subject"", e.""Body"", e.""HtmlBody"",
                       e.""From"", e.""To"", e.""Cc"", e.""Bcc"", e.""SentDate"", e.""ReceivedDate"",
                       e.""IsOutgoing"", e.""HasAttachments"", e.""FolderName"", e.""IsLocked"",
                       ma.""Id"" as ""AccountId"", ma.""Name"" as ""AccountName"", ma.""EmailAddress"" as ""AccountEmail""
                FROM mail_archiver.""ArchivedEmails"" e
                INNER JOIN mail_archiver.""MailAccounts"" ma ON e.""MailAccountId"" = ma.""Id""
                {whereClause}
                {orderByClause}
                LIMIT {take} OFFSET {skip}";

            var emails = await ExecuteDataQueryAsync(dataSql, CloneParameters(parameters));

            return (emails, totalCount);
        }

        private async Task<T> ExecuteScalarQueryAsync<T>(string sql, List<Npgsql.NpgsqlParameter> parameters)
        {
            using var connection = new Npgsql.NpgsqlConnection(_context.Database.GetConnectionString());
            await connection.OpenAsync();

            using var command = new Npgsql.NpgsqlCommand(sql, connection);
            foreach (var parameter in parameters)
            {
                command.Parameters.Add(parameter);
            }

            var result = await command.ExecuteScalarAsync();
            return (T)Convert.ChangeType(result, typeof(T));
        }

        private async Task<List<ArchivedEmail>> ExecuteDataQueryAsync(string sql, List<Npgsql.NpgsqlParameter> parameters)
        {
            var emails = new List<ArchivedEmail>();

            using var connection = new Npgsql.NpgsqlConnection(_context.Database.GetConnectionString());
            await connection.OpenAsync();

            using var command = new Npgsql.NpgsqlCommand(sql, connection);
            foreach (var parameter in parameters)
            {
                command.Parameters.Add(parameter);
            }

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var email = new ArchivedEmail
                {
                    Id = reader.GetInt32(reader.GetOrdinal("Id")),
                    MailAccountId = reader.GetInt32(reader.GetOrdinal("MailAccountId")),
                    MessageId = reader.IsDBNull(reader.GetOrdinal("MessageId")) ? "" : reader.GetString(reader.GetOrdinal("MessageId")),
                    Subject = reader.IsDBNull(reader.GetOrdinal("Subject")) ? "" : reader.GetString(reader.GetOrdinal("Subject")),
                    Body = reader.IsDBNull(reader.GetOrdinal("Body")) ? "" : reader.GetString(reader.GetOrdinal("Body")),
                    HtmlBody = reader.IsDBNull(reader.GetOrdinal("HtmlBody")) ? "" : reader.GetString(reader.GetOrdinal("HtmlBody")),
                    From = reader.IsDBNull(reader.GetOrdinal("From")) ? "" : reader.GetString(reader.GetOrdinal("From")),
                    To = reader.IsDBNull(reader.GetOrdinal("To")) ? "" : reader.GetString(reader.GetOrdinal("To")),
                    Cc = reader.IsDBNull(reader.GetOrdinal("Cc")) ? "" : reader.GetString(reader.GetOrdinal("Cc")),
                    Bcc = reader.IsDBNull(reader.GetOrdinal("Bcc")) ? "" : reader.GetString(reader.GetOrdinal("Bcc")),
                    SentDate = reader.GetDateTime(reader.GetOrdinal("SentDate")),
                    ReceivedDate = reader.GetDateTime(reader.GetOrdinal("ReceivedDate")),
                    IsOutgoing = reader.GetBoolean(reader.GetOrdinal("IsOutgoing")),
                    HasAttachments = reader.GetBoolean(reader.GetOrdinal("HasAttachments")),
                    FolderName = reader.IsDBNull(reader.GetOrdinal("FolderName")) ? "" : reader.GetString(reader.GetOrdinal("FolderName")),
                    IsLocked = reader.GetBoolean(reader.GetOrdinal("IsLocked")),
                    MailAccount = new MailAccount
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("AccountId")),
                        Name = reader.IsDBNull(reader.GetOrdinal("AccountName")) ? "" : reader.GetString(reader.GetOrdinal("AccountName")),
                        EmailAddress = reader.IsDBNull(reader.GetOrdinal("AccountEmail")) ? "" : reader.GetString(reader.GetOrdinal("AccountEmail"))
                    }
                };
                emails.Add(email);
            }

            return emails;
        }

        private (string tsQuery, List<string> phrases, Dictionary<string, List<string>> fieldSearches, Dictionary<string, List<string>> fieldPhrases) ParseSearchTermForTsQuery(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return (null, new List<string>(), new Dictionary<string, List<string>>(), new Dictionary<string, List<string>>());

            var phrases = new List<string>();
            var individualWords = new List<string>();
            var fieldSearches = new Dictionary<string, List<string>>();
            var fieldPhrases = new Dictionary<string, List<string>>();
            var validFields = new HashSet<string> { "subject", "body", "from", "to" };

            var regex = new Regex(@"""([^""]*)""|(\w+):(""([^""]*)""|(\S+))|(\S+)", RegexOptions.None);
            var matches = regex.Matches(searchTerm);

            foreach (Match match in matches)
            {
                if (match.Groups[1].Success)
                {
                    var phrase = match.Groups[1].Value.Trim();
                    if (!string.IsNullOrEmpty(phrase))
                        phrases.Add(phrase);
                }
                else if (match.Groups[2].Success)
                {
                    var field = match.Groups[2].Value.ToLower().Trim();
                    if (validFields.Contains(field))
                    {
                        if (match.Groups[4].Success)
                        {
                            var fieldPhrase = match.Groups[4].Value.Trim();
                            if (!string.IsNullOrEmpty(fieldPhrase))
                            {
                                if (!fieldPhrases.ContainsKey(field))
                                    fieldPhrases[field] = new List<string>();
                                fieldPhrases[field].Add(fieldPhrase);
                            }
                        }
                        else if (match.Groups[5].Success)
                        {
                            var fieldTerm = match.Groups[5].Value.Trim();
                            if (!string.IsNullOrEmpty(fieldTerm))
                            {
                                var sanitized = Regex.Replace(fieldTerm, @"[&|!():\*]", "", RegexOptions.None);
                                if (!string.IsNullOrEmpty(sanitized))
                                {
                                    if (!fieldSearches.ContainsKey(field))
                                        fieldSearches[field] = new List<string>();
                                    fieldSearches[field].Add(sanitized);
                                }
                            }
                        }
                    }
                }
                else if (match.Groups[6].Success)
                {
                    var word = match.Groups[6].Value.Trim();
                    if (!string.IsNullOrEmpty(word))
                    {
                        var sanitized = Regex.Replace(word, @"[&|!():\*]", "", RegexOptions.None);
                        if (!string.IsNullOrEmpty(sanitized))
                            individualWords.Add(sanitized);
                    }
                }
            }

            string tsQuery = null;
            if (individualWords.Any())
            {
                var escapedTerms = individualWords.Select(t => t.Replace("'", "''"));
                tsQuery = string.Join(" & ", escapedTerms);
            }

            return (tsQuery, phrases, fieldSearches, fieldPhrases);
        }

        private string GetColumnNameForField(string fieldName)
        {
            return fieldName.ToLower() switch
            {
                "subject" => "Subject",
                "body" => "Body",
                "from" => "From",
                "to" => "To",
                _ => null
            };
        }

        private string GetOrderByClause(string sortBy, string sortOrder)
        {
            var columnName = sortBy?.ToLower() switch
            {
                "subject" => "Subject",
                "from" => "From",
                "to" => "To",
                "sentdate" => "SentDate",
                "receiveddate" => "ReceivedDate",
                _ => "SentDate"
            };

            var direction = sortOrder?.ToLower() == "asc" ? "ASC" : "DESC";
            return $@"ORDER BY e.""{columnName}"" {direction}";
        }

        private List<Npgsql.NpgsqlParameter> CloneParameters(List<Npgsql.NpgsqlParameter> parameters)
        {
            var clonedParameters = new List<Npgsql.NpgsqlParameter>();
            foreach (var param in parameters)
            {
                clonedParameters.Add(new Npgsql.NpgsqlParameter(param.ParameterName, param.Value));
            }
            return clonedParameters;
        }

        private async Task<(List<ArchivedEmail> Emails, int TotalCount)> SearchEmailsEFAsync(
            string searchTerm,
            DateTime? fromDate,
            DateTime? toDate,
            int? accountId,
            string folderName,
            bool? isOutgoing,
            int skip,
            int take,
            List<int> allowedAccountIds = null)
        {
            var baseQuery = _context.ArchivedEmails.AsNoTracking().AsQueryable();

            if (allowedAccountIds != null)
            {
                if (allowedAccountIds.Any())
                    baseQuery = baseQuery.Where(e => allowedAccountIds.Contains(e.MailAccountId));
                else
                    baseQuery = baseQuery.Where(e => false);
            }

            if (accountId.HasValue)
            {
                if (allowedAccountIds != null && allowedAccountIds.Any() && !allowedAccountIds.Contains(accountId.Value))
                    return (new List<ArchivedEmail>(), 0);
                baseQuery = baseQuery.Where(e => e.MailAccountId == accountId.Value);
            }

            if (fromDate.HasValue)
                baseQuery = baseQuery.Where(e => e.SentDate >= fromDate.Value);

            if (toDate.HasValue)
                baseQuery = baseQuery.Where(e => e.SentDate <= toDate.Value.AddDays(1).AddSeconds(-1));

            if (isOutgoing.HasValue)
                baseQuery = baseQuery.Where(e => e.IsOutgoing == isOutgoing.Value);

            if (!string.IsNullOrEmpty(folderName))
                baseQuery = baseQuery.Where(e => e.FolderName == folderName);

            IQueryable<ArchivedEmail> searchQuery = baseQuery;
            if (!string.IsNullOrEmpty(searchTerm))
            {
                var (tsQuery, phrases, fieldSearches, fieldPhrases) = ParseSearchTermForTsQuery(searchTerm);

                if (!string.IsNullOrEmpty(tsQuery))
                {
                    var words = tsQuery.Split('&', StringSplitOptions.RemoveEmptyEntries)
                                      .Select(w => w.Trim().Replace("''", "'"))
                                      .ToList();

                    foreach (var word in words)
                    {
                        var escapedWord = word.Replace("'", "''");
                        searchQuery = searchQuery.Where(e =>
                            EF.Functions.ILike(e.Subject, $"%{escapedWord}%") ||
                            EF.Functions.ILike(e.From, $"%{escapedWord}%") ||
                            EF.Functions.ILike(e.To, $"%{escapedWord}%") ||
                            EF.Functions.ILike(e.Body, $"%{escapedWord}%") ||
                            EF.Functions.ILike(e.Cc, $"%{escapedWord}%") ||
                            EF.Functions.ILike(e.Bcc, $"%{escapedWord}%")
                        );
                    }
                }

                foreach (var phrase in phrases)
                {
                    searchQuery = searchQuery.Where(e =>
                        (e.Subject != null && e.Subject.ToLower().Contains(phrase.ToLower())) ||
                        (e.From != null && e.From.ToLower().Contains(phrase.ToLower())) ||
                        (e.To != null && e.To.ToLower().Contains(phrase.ToLower())) ||
                        (e.Body != null && e.Body.ToLower().Contains(phrase.ToLower())) ||
                        (e.Cc != null && e.Cc.ToLower().Contains(phrase.ToLower())) ||
                        (e.Bcc != null && e.Bcc.ToLower().Contains(phrase.ToLower()))
                    );
                }

                foreach (var fieldSearch in fieldSearches)
                {
                    var field = fieldSearch.Key;
                    var terms = fieldSearch.Value;

                    foreach (var term in terms)
                    {
                        var escapedTerm = term.Replace("'", "''");
                        switch (field.ToLower())
                        {
                            case "subject":
                                searchQuery = searchQuery.Where(e => e.Subject != null && EF.Functions.ILike(e.Subject, $"%{escapedTerm}%"));
                                break;
                            case "body":
                                searchQuery = searchQuery.Where(e => e.Body != null && EF.Functions.ILike(e.Body, $"%{escapedTerm}%"));
                                break;
                            case "from":
                                searchQuery = searchQuery.Where(e => e.From != null && EF.Functions.ILike(e.From, $"%{escapedTerm}%"));
                                break;
                            case "to":
                                searchQuery = searchQuery.Where(e => e.To != null && EF.Functions.ILike(e.To, $"%{escapedTerm}%"));
                                break;
                        }
                    }
                }

                foreach (var fieldPhrase in fieldPhrases)
                {
                    var field = fieldPhrase.Key;
                    var fieldPhrasesList = fieldPhrase.Value;

                    foreach (var phrase in fieldPhrasesList)
                    {
                        switch (field.ToLower())
                        {
                            case "subject":
                                searchQuery = searchQuery.Where(e => e.Subject != null && e.Subject.ToLower().Contains(phrase.ToLower()));
                                break;
                            case "body":
                                searchQuery = searchQuery.Where(e => e.Body != null && e.Body.ToLower().Contains(phrase.ToLower()));
                                break;
                            case "from":
                                searchQuery = searchQuery.Where(e => e.From != null && e.From.ToLower().Contains(phrase.ToLower()));
                                break;
                            case "to":
                                searchQuery = searchQuery.Where(e => e.To != null && e.To.ToLower().Contains(phrase.ToLower()));
                                break;
                        }
                    }
                }
            }

            var totalCount = await searchQuery.CountAsync();
            var emails = await searchQuery
                .Include(e => e.MailAccount)
                .OrderByDescending(e => e.SentDate)
                .Skip(skip)
                .Take(take)
                .ToListAsync();

            return (emails, totalCount);
        }

        #endregion

        #region Email Count

        public async Task<int> GetEmailCountByAccountAsync(int accountId)
        {
            return await _context.ArchivedEmails.CountAsync(e => e.MailAccountId == accountId);
        }

        #endregion

        #region Export Methods

        public async Task<byte[]> ExportEmailsAsync(ExportViewModel parameters, List<int> allowedAccountIds = null)
        {
            using var ms = new MemoryStream();

            if (parameters.EmailId.HasValue)
            {
                var email = await _context.ArchivedEmails
                    .Include(e => e.MailAccount)
                    .Include(e => e.Attachments)
                    .FirstOrDefaultAsync(e => e.Id == parameters.EmailId.Value);

                if (email == null)
                {
                    throw new InvalidOperationException($"Email with ID {parameters.EmailId.Value} not found");
                }

                switch (parameters.Format)
                {
                    case ExportFormat.Csv:
                        await ExportSingleEmailAsCsv(email, ms);
                        break;
                    case ExportFormat.Json:
                        await ExportSingleEmailAsJson(email, ms);
                        break;
                    case ExportFormat.Eml:
                        await ExportSingleEmailAsEml(email, ms);
                        break;
                }
            }
            else
            {
                var searchTerm = parameters.SearchTerm ?? string.Empty;
                var (emails, _) = await SearchEmailsAsync(
                    searchTerm,
                    parameters.FromDate,
                    parameters.ToDate,
                    parameters.SelectedAccountId,
                    null,
                    parameters.IsOutgoing,
                    0,
                    10000,
                    allowedAccountIds);

                switch (parameters.Format)
                {
                    case ExportFormat.Csv:
                        await ExportMultipleEmailsAsCsv(emails, ms);
                        break;
                    case ExportFormat.Json:
                        await ExportMultipleEmailsAsJson(emails, ms);
                        break;
                }
            }

            ms.Position = 0;
            return ms.ToArray();
        }

        private async Task ExportSingleEmailAsCsv(ArchivedEmail email, MemoryStream ms)
        {
            using var writer = new StreamWriter(ms, Encoding.UTF8, leaveOpen: true);
            writer.WriteLine("Subject;From;To;Date;Account;Direction;Message Text");

            var subject = email.Subject.Replace("\"", "\"\"").Replace(";", ",");
            var from = email.From.Replace("\"", "\"\"").Replace(";", ",");
            var to = email.To.Replace("\"", "\"\"").Replace(";", ",");
            var sentDate = email.SentDate.ToString("yyyy-MM-dd HH:mm:ss");
            var account = email.MailAccount?.Name.Replace("\"", "\"\"").Replace(";", ",") ?? "Unknown";
            var direction = email.IsOutgoing ? "Outgoing" : "Incoming";
            var body = email.Body?.Replace("\r", " ").Replace("\n", " ")
                .Replace("\"", "\"\"").Replace(";", ",") ?? "";

            writer.WriteLine($"\"{subject}\";\"{from}\";\"{to}\";\"{sentDate}\";\"{account}\";\"{direction}\";\"{body}\"");
            await writer.FlushAsync();
        }

        private async Task ExportSingleEmailAsJson(ArchivedEmail email, MemoryStream ms)
        {
            var exportData = new
            {
                Id = email.Id,
                Subject = email.Subject,
                From = email.From,
                To = email.To,
                Cc = email.Cc,
                Bcc = email.Bcc,
                SentDate = email.SentDate,
                ReceivedDate = email.ReceivedDate,
                AccountName = email.MailAccount?.Name,
                FolderName = email.FolderName,
                IsOutgoing = email.IsOutgoing,
                HasAttachments = email.HasAttachments,
                MessageId = email.MessageId,
                Body = email.Body,
                HtmlBody = email.HtmlBody,
                Attachments = email.Attachments?.Select(a => new
                {
                    FileName = a.FileName,
                    ContentType = a.ContentType,
                    Size = a.Size
                }).ToList()
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            await JsonSerializer.SerializeAsync(ms, exportData, options);
        }

        private async Task ExportSingleEmailAsEml(ArchivedEmail email, MemoryStream ms)
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

            if (!string.IsNullOrEmpty(email.Bcc))
            {
                foreach (var bcc in email.Bcc.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    try { message.Bcc.Add(InternetAddress.Parse(bcc.Trim())); }
                    catch { continue; }
                }
            }

            // Import raw headers if available (for forensic/compliance purposes)
            if (!string.IsNullOrEmpty(email.RawHeaders))
            {
                try
                {
                    // Parse and add raw headers to preserve original headers
                    using var reader = new StringReader(email.RawHeaders);
                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        var colonIndex = line.IndexOf(':');
                        if (colonIndex > 0)
                        {
                            var headerName = line.Substring(0, colonIndex).Trim();
                            var headerValue = line.Substring(colonIndex + 1).Trim();
                            
                            // Skip headers that are already set by MimeMessage properties
                            // to avoid duplicates
                            var skipHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                            {
                                "Subject", "From", "To", "Cc", "Bcc", "Date", "Message-ID",
                                "MIME-Version", "Content-Type"
                            };
                            
                            if (!skipHeaders.Contains(headerName))
                            {
                                try
                                {
                                    message.Headers.Add(headerName, headerValue);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogDebug("Could not add header {HeaderName}: {Error}", headerName, ex.Message);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing raw headers for email {EmailId}", email.Id);
                }
            }

            // Priority: Original body (with null bytes) > Untruncated body > Regular body
            // Use OriginalBody if available (contains original content including null bytes)
            var htmlBodyToExport = email.OriginalBodyHtml != null
                ? Encoding.UTF8.GetString(email.OriginalBodyHtml)
                : (!string.IsNullOrEmpty(email.BodyUntruncatedHtml)
                    ? email.BodyUntruncatedHtml
                    : email.HtmlBody);

            var textBodyToExport = email.OriginalBodyText != null
                ? Encoding.UTF8.GetString(email.OriginalBodyText)
                : (!string.IsNullOrEmpty(email.BodyUntruncatedText)
                    ? email.BodyUntruncatedText
                    : email.Body);

            var body = !string.IsNullOrEmpty(htmlBodyToExport)
                ? new TextPart("html") { Text = htmlBodyToExport }
                : new TextPart("plain") { Text = textBodyToExport };

            if (email.Attachments.Any())
            {
                var multipart = new Multipart("mixed");
                var inlineAttachments = email.Attachments.Where(a => !string.IsNullOrEmpty(a.ContentId)).ToList();
                var regularAttachments = email.Attachments.Where(a => string.IsNullOrEmpty(a.ContentId)).ToList();

                if (inlineAttachments.Any() && !string.IsNullOrEmpty(email.HtmlBody))
                {
                    var related = new Multipart("related");
                    related.Add(body);

                    foreach (var attachment in inlineAttachments)
                    {
                        var mimePart = new MimePart(attachment.ContentType)
                        {
                            Content = new MimeContent(new MemoryStream(attachment.Content)),
                            ContentId = attachment.ContentId,
                            ContentDisposition = new ContentDisposition(ContentDisposition.Inline),
                            ContentTransferEncoding = ContentEncoding.Base64,
                            FileName = attachment.FileName
                        };
                        related.Add(mimePart);
                    }

                    multipart.Add(related);
                }
                else
                {
                    multipart.Add(body);
                }

                foreach (var attachment in regularAttachments)
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

            await Task.Run(() => message.WriteTo(ms));
        }

        private async Task ExportMultipleEmailsAsCsv(List<ArchivedEmail> emails, MemoryStream ms)
        {
            using var writer = new StreamWriter(ms, Encoding.UTF8, leaveOpen: true);
            writer.WriteLine("Subject;From;To;Date;Account;Direction;Message Text");

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
            await writer.FlushAsync();
        }

        private async Task ExportMultipleEmailsAsJson(List<ArchivedEmail> emails, MemoryStream ms)
        {
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
        }

        #endregion

        #region Dashboard Methods

        public async Task<DashboardViewModel> GetDashboardStatisticsAsync()
        {
            var model = new DashboardViewModel();

            model.TotalEmails = await _context.ArchivedEmails.CountAsync();
            model.TotalAccounts = await _context.MailAccounts.CountAsync();
            model.TotalAttachments = await _context.EmailAttachments.CountAsync();

            var totalDatabaseSizeBytes = await GetDatabaseSizeAsync();
            model.TotalStorageUsed = FormatFileSize(totalDatabaseSizeBytes);

            model.EmailsPerAccount = await _context.MailAccounts
                .Select(a => new AccountStatistics
                {
                    AccountName = a.Name,
                    EmailAddress = a.EmailAddress,
                    EmailCount = a.ArchivedEmails.Count,
                    LastSyncTime = a.LastSync,
                    IsEnabled = a.IsEnabled
                })
                .ToListAsync();

            var now = DateTime.UtcNow;
            var startDate = now.AddMonths(-11).Date;
            startDate = new DateTime(startDate.Year, startDate.Month, 1);
            var months = new List<EmailCountByPeriod>();
            for (int i = 0; i < 12; i++)
            {
                var currentMonth = startDate.AddMonths(i);
                var nextMonth = currentMonth.AddMonths(1);

                int count;
                if (i == 11)
                {
                    count = await _context.ArchivedEmails
                        .Where(e => e.SentDate >= currentMonth && e.SentDate <= now)
                        .CountAsync();
                }
                else
                {
                    count = await _context.ArchivedEmails
                        .Where(e => e.SentDate >= currentMonth && e.SentDate < nextMonth)
                        .CountAsync();
                }

                months.Add(new EmailCountByPeriod
                {
                    Period = $"{CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(currentMonth.Month)} {currentMonth.Year}",
                    Count = count
                });
            }
            model.EmailsByMonth = months;

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

            model.RecentEmails = await _context.ArchivedEmails
                .Include(e => e.MailAccount)
                .OrderByDescending(e => e.SentDate)
                .Take(10)
                .ToListAsync();

            return model;
        }

        private async Task<long> GetDatabaseSizeAsync()
        {
            try
            {
                using var connection = new Npgsql.NpgsqlConnection(_context.Database.GetConnectionString());
                await connection.OpenAsync();

                var sql = "SELECT pg_database_size(current_database())";

                using var command = new Npgsql.NpgsqlCommand(sql, connection);
                var result = await command.ExecuteScalarAsync();

                return Convert.ToInt64(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting database size: {Message}", ex.Message);
                return await _context.EmailAttachments.SumAsync(a => (long)a.Size);
            }
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

        #endregion

        #region Archiving

        public async Task<bool> ArchiveEmailAsync(MailAccount account, MimeMessage message, bool isOutgoing, string? folderName = null)
        {
            // Extract date with fallback handling for malformed Date headers
            var emailDate = ExtractEmailDate(message);

            // Extract raw headers for forensic/compliance purposes
                var rawHeaders = ExtractRawHeaders(message);
                
                // Clean raw headers to remove null bytes (prevent PostgreSQL UTF-8 errors)
                if (!string.IsNullOrEmpty(rawHeaders))
                {
                    rawHeaders = CleanText(rawHeaders);
                }

                // Check if this email is already archived
            var messageId = message.MessageId ??
                $"{message.From}-{message.To}-{message.Subject}-{emailDate.Ticks}";

            var existingEmail = await _context.ArchivedEmails
                .FirstOrDefaultAsync(e => e.MessageId == messageId && e.MailAccountId == account.Id);

            if (existingEmail != null)
            {
                // E-Mail existiert bereits, prüfen ob der Ordner geändert wurde
                var cleanFolderName = CleanText(folderName ?? string.Empty);
                if (existingEmail.FolderName != cleanFolderName)
                {
                    // Ordner hat sich geändert, aktualisieren
                    var oldFolder = existingEmail.FolderName;
                    existingEmail.FolderName = cleanFolderName;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Updated folder for existing email: {Subject} from '{OldFolder}' to '{NewFolder}'",
                        existingEmail.Subject, oldFolder, cleanFolderName);
                }
                return false; // E-Mail existiert bereits
            }

            try
            {
                // Convert timestamp to configured display timezone
                var convertedSentDate = _dateTimeHelper.ConvertToDisplayTimeZone(emailDate);
                var subject = CleanText(message.Subject ?? "(No Subject)");
                // Extract email address from From field
                var fromAddress = message.From?.FirstOrDefault() as MailboxAddress;
                var from = CleanText(fromAddress?.Address ?? string.Empty);
                // Extract email addresses from To field
                var toAddresses = message.To?.Select(m => m as MailboxAddress).Where(m => m != null).Select(m => m.Address) ?? new List<string>();
                var to = CleanText(string.Join(", ", toAddresses));
                // Extract email addresses from Cc field
                var ccAddresses = message.Cc?.Select(m => m as MailboxAddress).Where(m => m != null).Select(m => m.Address) ?? new List<string>();
                var cc = CleanText(string.Join(", ", ccAddresses));
                // Extract email addresses from Bcc field
                var bccAddresses = message.Bcc?.Select(m => m as MailboxAddress).Where(m => m != null).Select(m => m.Address) ?? new List<string>();
                var bcc = CleanText(string.Join(", ", bccAddresses));

                // Extract text and HTML body preserving original encoding
                var body = string.Empty;
                var htmlBody = string.Empty;

                // Store raw body content BEFORE cleaning to detect null bytes
                // This preserves the original content including any null bytes for faithful export
                var rawTextBody = message.TextBody;
                var rawHtmlBody = message.HtmlBody;
                
                // Check if original bodies contain null bytes - if so, store them as byte arrays
                var hasNullBytesInText = !string.IsNullOrEmpty(rawTextBody) && rawTextBody.Contains('\0');
                var hasNullBytesInHtml = !string.IsNullOrEmpty(rawHtmlBody) && rawHtmlBody.Contains('\0');
                
                // Keep references to the original unmodified body content
                // These will be stored in OriginalBodyText/Html (as byte[]) if they differ from the stored version
                // IMPORTANT: Clean the original bodies to remove null bytes for the cleaned version
                var originalTextBody = !string.IsNullOrEmpty(rawTextBody) ? CleanText(rawTextBody) : null;
                var originalHtmlBody = !string.IsNullOrEmpty(rawHtmlBody) ? CleanText(rawHtmlBody) : null;

                // Handle text body - use original content directly to preserve encoding
                if (!string.IsNullOrEmpty(message.TextBody))
                {
                    var cleanedTextBody = CleanText(message.TextBody);
                    // Check if text body needs truncation for tsvector compatibility
                    // Set to 500KB to ensure total of all fields stays under 1MB tsvector limit
                    if (Encoding.UTF8.GetByteCount(cleanedTextBody) > 500_000)
                    {
                        body = TruncateTextForStorage(cleanedTextBody, 500_000);
                    }
                    else
                    {
                        body = cleanedTextBody;
                    }
                }
                else if (!string.IsNullOrEmpty(message.HtmlBody))
                {
                    // If no TextBody, try to extract text from HTML body
                    // For BodyUntruncatedText, the original source is HtmlBody in this case
                    originalTextBody = message.HtmlBody;
                    var cleanedHtmlAsText = CleanText(message.HtmlBody);
                    // Check if HTML-as-text body needs truncation for tsvector compatibility
                    // Set to 500KB to ensure total of all fields stays under 1MB tsvector limit
                    if (Encoding.UTF8.GetByteCount(cleanedHtmlAsText) > 500_000)
                    {
                        body = TruncateTextForStorage(cleanedHtmlAsText, 500_000);
                    }
                    else
                    {
                        body = cleanedHtmlAsText;
                    }
                }

                // Handle HTML body - preserve original encoding (keep cid: references for inline images)
                if (!string.IsNullOrEmpty(message.HtmlBody))
                {
                    // Keep the original HTML body with cid: references
                    htmlBody = CleanText(message.HtmlBody);

                    // Check if HTML body will be truncated
                    if (htmlBody.Length > 1_000_000)
                    {
                        htmlBody = CleanHtmlForStorage(htmlBody);
                    }
                }

                var cleanMessageId = CleanText(messageId);
                var cleanFolderName = CleanText(folderName ?? string.Empty);

                // Ensure individual fields don't exceed reasonable limits for tsvector
                // This prevents tsvector size errors when all fields are concatenated
                subject = TruncateFieldForTsvector(subject, 50_000); // ~50KB for subject
                from = TruncateFieldForTsvector(from, 10_000); // ~10KB for from
                to = TruncateFieldForTsvector(to, 50_000); // ~50KB for to (can be many recipients)
                cc = TruncateFieldForTsvector(cc, 50_000); // ~50KB for cc
                bcc = TruncateFieldForTsvector(bcc, 50_000); // ~50KB for bcc
                                                             // Body already truncated above to 500KB

                // Final safety check: ensure total size for tsvector doesn't exceed limit
                var totalTsvectorSize = Encoding.UTF8.GetByteCount(subject) +
                                       Encoding.UTF8.GetByteCount(body) +
                                       Encoding.UTF8.GetByteCount(from) +
                                       Encoding.UTF8.GetByteCount(to) +
                                       Encoding.UTF8.GetByteCount(cc) +
                                       Encoding.UTF8.GetByteCount(bcc);

                // PostgreSQL tsvector max is ~1MB (1048575 bytes), use 900KB as safe limit
                const int maxTsvectorSize = 900_000;
                if (totalTsvectorSize > maxTsvectorSize)
                {
                    _logger.LogWarning("Email fields exceed tsvector limit ({TotalSize} > {MaxSize}), truncating body further",
                        totalTsvectorSize, maxTsvectorSize);

                    // Calculate how much we need to reduce the body
                    var otherFieldsSize = totalTsvectorSize - Encoding.UTF8.GetByteCount(body);
                    var maxBodySize = maxTsvectorSize - otherFieldsSize - 10_000; // 10KB safety buffer

                    if (maxBodySize > 0 && Encoding.UTF8.GetByteCount(body) > maxBodySize)
                    {
                        body = TruncateTextForStorage(body, maxBodySize);
                    }
                    else if (maxBodySize <= 0)
                    {
                        // Other fields alone exceed limit, truncate body completely
                        _logger.LogError("Other email fields alone exceed tsvector limit, body will be saved as attachment only");
                        body = "[Body too large - saved as attachment]";
                    }
                }

                // Sammle ALLE Anhänge einschließlich inline Images
                var allAttachments = new List<MimePart>();
                CollectAllAttachments(message.Body, allAttachments);

                // Determine if the email is outgoing by comparing the From address with the account's email address
                bool isOutgoingEmail = !string.IsNullOrEmpty(from) &&
                                      !string.IsNullOrEmpty(account.EmailAddress) &&
                                      from.Equals(account.EmailAddress, StringComparison.OrdinalIgnoreCase);

                // Additionally check if the folder indicates outgoing mail
                bool isOutgoingFolder = IsOutgoingFolderByName(folderName);

                // Additionally check if the folder is a drafts folder to exclude it from outgoing emails
                bool isDraftsFolder = IsDraftsFolder(folderName);

                var archivedEmail = new ArchivedEmail
                {
                    MailAccountId = account.Id,
                    MessageId = cleanMessageId,
                    Subject = subject,
                    From = from,
                    To = to,
                    Cc = cc,
                    Bcc = bcc,
                    SentDate = convertedSentDate,
                    ReceivedDate = DateTime.UtcNow,
                    IsOutgoing = (isOutgoingEmail || isOutgoingFolder) && !isDraftsFolder,
                    HasAttachments = allAttachments.Any(),
                    Body = body,
                    HtmlBody = htmlBody,
                    // LEGACY: BodyUntruncated fields are no longer populated for new emails (kept for backward compatibility)
                    // Original body content is now stored in OriginalBody* fields (as byte[]) for both truncation AND null-byte cases
                    BodyUntruncatedText = null,  // Not populated for new emails - use OriginalBodyText instead
                    BodyUntruncatedHtml = null,  // Not populated for new emails - use OriginalBodyHtml instead
                    // Store original body as byte array for faithful export/restore
                    // Populated when: (1) original contained null bytes, OR (2) original was truncated
                    OriginalBodyText = (hasNullBytesInText || (!string.IsNullOrEmpty(originalTextBody) && originalTextBody != body)) 
                        ? Encoding.UTF8.GetBytes(hasNullBytesInText ? rawTextBody! : originalTextBody!) : null,
                    OriginalBodyHtml = (hasNullBytesInHtml || (!string.IsNullOrEmpty(originalHtmlBody) && originalHtmlBody != htmlBody)) 
                        ? Encoding.UTF8.GetBytes(hasNullBytesInHtml ? rawHtmlBody! : originalHtmlBody!) : null,
                    FolderName = cleanFolderName,
                    RawHeaders = rawHeaders, // Store raw headers for forensic/compliance purposes
                    Attachments = new List<EmailAttachment>() // Initialize collection for hash calculation
                };

                // CRITICAL: Prepare attachments BEFORE calculating hash
                // This ensures the hash includes the attachment content
                var emailAttachments = new List<EmailAttachment>();
                if (allAttachments.Any())
                {
                    foreach (var attachment in allAttachments)
                    {
                        try
                        {
                            using var ms = new MemoryStream();
                            await attachment.Content.DecodeToAsync(ms);

                            var fileName = attachment.FileName;
                            if (string.IsNullOrEmpty(fileName))
                            {
                                if (!string.IsNullOrEmpty(attachment.ContentId))
                                {
                                    var extension = GetFileExtensionFromContentType(attachment.ContentType?.MimeType);
                                    var cleanContentId = attachment.ContentId.Trim('<', '>');
                                    fileName = $"inline_{cleanContentId}{extension}";
                                }
                                else if (attachment.ContentType?.MediaType?.StartsWith("image/") == true)
                                {
                                    var extension = GetFileExtensionFromContentType(attachment.ContentType.MimeType);
                                    fileName = $"inline_image_{Guid.NewGuid().ToString("N")[..8]}{extension}";
                                }
                                else
                                {
                                    var extension = GetFileExtensionFromContentType(attachment.ContentType?.MimeType);
                                    fileName = $"attachment_{Guid.NewGuid().ToString("N")[..8]}{extension}";
                                }
                            }

                            var cleanFileName = CleanText(fileName);
                            var contentType = CleanText(attachment.ContentType?.MimeType ?? "application/octet-stream");
                            var contentId = !string.IsNullOrEmpty(attachment.ContentId) ? attachment.ContentId.Trim() : null;

                            var emailAttachment = new EmailAttachment
                            {
                                FileName = cleanFileName,
                                ContentType = contentType,
                                ContentId = contentId,
                                Content = ms.ToArray(),
                                Size = ms.Length
                            };

                            emailAttachments.Add(emailAttachment);
                            archivedEmail.Attachments.Add(emailAttachment); // Add to collection for hash calculation
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to process attachment: FileName={FileName}, ContentType={ContentType}",
                                attachment.FileName, attachment.ContentType?.MimeType);
                        }
                    }
                }

                try
                {
                    _context.ArchivedEmails.Add(archivedEmail);
                    await _context.SaveChangesAsync();

                    // Attachments are already saved via EF relationship (cascade)
                    _logger.LogInformation("Successfully saved email with {Count} attachments", emailAttachments.Count);

                    _logger.LogInformation(
                        "Archived email: {Subject}, From: {From}, To: {To}, Account: {AccountName}, Attachments: {AttachmentCount}, OriginalPreserved: {OriginalPreserved}",
                        archivedEmail.Subject, archivedEmail.From, archivedEmail.To, account.Name, allAttachments.Count,
                        archivedEmail.BodyUntruncatedText != null || archivedEmail.BodyUntruncatedHtml != null ? "Yes" : "No");

                    return true; // Neue E-Mail erfolgreich archiviert
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error saving archived email to database: {Subject}, {Message}", subject, ex.Message);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error archiving email: Subject={Subject}, From={From}, Error={Message}",
                    message.Subject, message.From, ex.Message);
                return false;
            }
        }

        private void CollectAllAttachments(MimeEntity entity, List<MimePart> attachments)
        {
            if (entity is MimePart mimePart)
            {
                // Sammle normale Attachments
                if (mimePart.IsAttachment)
                {
                    attachments.Add(mimePart);
                    _logger.LogDebug("Found attachment: FileName={FileName}, ContentType={ContentType}",
                        mimePart.FileName, mimePart.ContentType?.MimeType);
                }
                // Sammle inline Images und andere inline Content
                else if (IsInlineContent(mimePart))
                {
                    attachments.Add(mimePart);
                    _logger.LogDebug("Found inline content: ContentId={ContentId}, ContentType={ContentType}, FileName={FileName}",
                        mimePart.ContentId, mimePart.ContentType?.MimeType, mimePart.FileName);
                }
            }
            else if (entity is Multipart multipart)
            {
                // Rekursiv durch alle Teile einer Multipart-Nachricht gehen
                foreach (var child in multipart)
                {
                    CollectAllAttachments(child, attachments);
                }
            }
            else if (entity is MessagePart messagePart)
            {
                // Auch in eingebetteten Nachrichten suchen
                CollectAllAttachments(messagePart.Message.Body, attachments);
            }
        }

        // Hilfsmethode um inline Content zu identifizieren
        private bool IsInlineContent(MimePart mimePart)
        {
            // Prüfe Content-Disposition auf "inline"
            if (mimePart.ContentDisposition?.Disposition?.Equals("inline", StringComparison.OrdinalIgnoreCase) == true)
            {
                _logger.LogDebug("Found inline content via Content-Disposition: inline - {ContentType}, ContentId: {ContentId}",
                    mimePart.ContentType?.MimeType, mimePart.ContentId);
                return true;
            }

            // Prüfe auf Content-ID (das wichtigste Kriterium für inline Images)
            // Content-ID ist der Standard-Indikator für inline Content, der in HTML via cid: referenziert wird
            if (!string.IsNullOrEmpty(mimePart.ContentId))
            {
                _logger.LogDebug("Found inline content via Content-ID: {ContentId}, ContentType: {ContentType}, FileName: {FileName}",
                    mimePart.ContentId, mimePart.ContentType?.MimeType, mimePart.FileName);
                return true;
            }

            // Fallback: Images ohne Content-ID aber mit inline disposition (falls Content-ID fehlt)
            if (mimePart.ContentType?.MediaType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true &&
                mimePart.ContentDisposition?.Disposition?.Equals("attachment", StringComparison.OrdinalIgnoreCase) != true)
            {
                // Nur wenn es nicht explizit als attachment markiert ist
                _logger.LogDebug("Found potential inline image without Content-ID: {ContentType}, FileName: {FileName}",
                    mimePart.ContentType?.MimeType, mimePart.FileName);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Resolves inline images in HTML by converting cid: references to data URLs
        /// </summary>
        private string ResolveInlineImagesInHtml(string htmlBody, List<EmailAttachment> attachments)
        {
            if (string.IsNullOrEmpty(htmlBody) || attachments == null || !attachments.Any())
                return htmlBody;

            var resultHtml = htmlBody;

            // Find all cid: references in the HTML
            var cidMatches = Regex.Matches(htmlBody, @"src\s*=\s*[""']cid:([^""']+)[""']", RegexOptions.IgnoreCase);

            foreach (Match match in cidMatches)
            {
                var cid = match.Groups[1].Value;

                // Find the corresponding attachment
                var attachment = attachments.FirstOrDefault(a =>
                    !string.IsNullOrEmpty(a.ContentId) &&
                    (a.ContentId.Equals($"<{cid}>", StringComparison.OrdinalIgnoreCase) ||
                     a.ContentId.Equals(cid, StringComparison.OrdinalIgnoreCase)));

                // If no attachment with ContentId found, try matching by filename
                if (attachment == null)
                {
                    attachment = attachments.FirstOrDefault(a =>
                        !string.IsNullOrEmpty(a.FileName) &&
                        (a.FileName.Equals($"inline_{cid}", StringComparison.OrdinalIgnoreCase) ||
                         a.FileName.StartsWith($"inline_{cid}.", StringComparison.OrdinalIgnoreCase) ||
                         a.FileName.Contains($"_{cid}")));
                }

                if (attachment != null && attachment.Content != null && attachment.Content.Length > 0)
                {
                    try
                    {
                        // Create a data URL for the inline image
                        var base64Content = Convert.ToBase64String(attachment.Content);
                        var dataUrl = $"data:{attachment.ContentType ?? "image/png"};base64,{base64Content}";

                        // Replace the cid: reference with the data URL
                        resultHtml = resultHtml.Replace(match.Groups[0].Value, $"src=\"{dataUrl}\"");

                        _logger.LogDebug("Resolved inline image with CID: {Cid} to data URL", cid);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to resolve inline image with CID: {Cid}", cid);
                    }
                }
                else
                {
                    _logger.LogWarning("Could not find attachment for CID: {Cid}", cid);
                }
            }

            return resultHtml;
        }

        // Hilfsmethode für Dateierweiterungen
        private string GetFileExtensionFromContentType(string? contentType)
        {
            return contentType?.ToLowerInvariant() switch
            {
                "image/jpeg" or "image/jpg" => ".jpg",
                "image/png" => ".png",
                "image/gif" => ".gif",
                "image/bmp" => ".bmp",
                "image/webp" => ".webp",
                "image/svg+xml" => ".svg",
                "image/tiff" => ".tiff",
                "image/ico" or "image/x-icon" => ".ico",
                "text/html" => ".html",
                "text/plain" => ".txt",
                "text/css" => ".css",
                "application/pdf" => ".pdf",
                "application/zip" => ".zip",
                "application/json" => ".json",
                "application/xml" => ".xml",
                _ => ".dat"
            };
        }

        #endregion

        #region Raw Headers Extraction

        /// <summary>
        /// Extracts all raw headers from a MimeMessage as a string.
        /// This includes all headers like Received, Return-Path, X-Headers, etc.
        /// Useful for forensic and compliance purposes.
        /// </summary>
        /// <param name="message">The MimeMessage to extract headers from</param>
        /// <returns>A string containing all raw headers, or null if extraction fails</returns>
        private string? ExtractRawHeaders(MimeMessage message)
        {
            try
            {
                if (message.Headers == null || !message.Headers.Any())
                {
                    return null;
                }

                var headersBuilder = new StringBuilder();

                // Iterate through all headers in their original order
                foreach (var header in message.Headers)
                {
                    // Format: "Header-Name: Header-Value\r\n"
                    headersBuilder.AppendLine($"{header.Field}: {header.Value}");
                }

                var rawHeaders = headersBuilder.ToString();

                // Limit size to prevent excessive storage (max ~100KB for headers)
                const int maxHeaderSize = 100_000;
                if (rawHeaders.Length > maxHeaderSize)
                {
                    _logger.LogWarning("Raw headers exceed {MaxSize} bytes, truncating", maxHeaderSize);
                    rawHeaders = rawHeaders.Substring(0, maxHeaderSize) + "\r\n[... Headers truncated due to size ...]";
                }

                _logger.LogDebug("Extracted {Count} raw headers ({Size} bytes) from email", 
                    message.Headers.Count, rawHeaders.Length);

                return rawHeaders;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract raw headers from email: {Message}", ex.Message);
                return null;
            }
        }

        #endregion

        #region Date Extraction

        /// <summary>
        /// Extracts the date from a MimeMessage with fallback handling for malformed Date headers.
        /// Tries the Date header first, then falls back to Received headers, and finally uses a default date.
        /// </summary>
        /// <param name="message">The MimeMessage to extract the date from</param>
        /// <returns>A DateTimeOffset representing the email's date</returns>
        private DateTimeOffset ExtractEmailDate(MimeMessage message)
        {
            // Try to get the date from the Date header
            try
            {
                if (message.Date != default)
                {
                    return message.Date;
                }
            }
            catch (ArgumentOutOfRangeException ex)
            {
                _logger.LogWarning("Malformed Date header in email Subject={Subject}, attempting fallback to Received headers. Error: {Error}",
                    message.Subject, ex.Message);
            }
            catch (FormatException ex)
            {
                _logger.LogWarning("Unparseable Date format in email Subject={Subject}, attempting fallback to Received headers. Error: {Error}",
                    message.Subject, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Error parsing Date header in email Subject={Subject}, attempting fallback to Received headers. Error: {Error}",
                    message.Subject, ex.Message);
            }

            // Fallback 1: Try to extract date from Received headers (newest first, which is typically at the top)
            try
            {
                var receivedHeaders = message.Headers.Where(h => h.Id == HeaderId.Received).ToList();
                
                // Iterate through Received headers (they're typically in reverse chronological order)
                // We want the oldest (last in the chain) which represents when the email was originally received
                for (int i = receivedHeaders.Count - 1; i >= 0; i--)
                {
                    var receivedHeader = receivedHeaders[i].Value;
                    var dateFromReceived = ExtractDateFromReceivedHeader(receivedHeader);
                    
                    if (dateFromReceived.HasValue)
                    {
                        _logger.LogInformation("Using date from Received header for email Subject={Subject}: {Date}",
                            message.Subject, dateFromReceived.Value);
                        return dateFromReceived.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Error extracting date from Received headers for email Subject={Subject}: {Error}",
                    message.Subject, ex.Message);
            }

            // Fallback 2: Try other date-related headers
            try
            {
                // Try Resent-Date header
                var resentDateHeader = message.Headers.FirstOrDefault(h => h.Id == HeaderId.ResentDate);
                if (resentDateHeader != null)
                {
                    var dateValue = ParseDateHeaderValue(resentDateHeader.Value);
                    if (dateValue.HasValue)
                    {
                        _logger.LogInformation("Using date from Resent-Date header for email Subject={Subject}: {Date}",
                            message.Subject, dateValue.Value);
                        return dateValue.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Error checking Resent-Date header: {Error}", ex.Message);
            }

            // Fallback 3: Use a default date (Unix epoch) to indicate unknown date
            _logger.LogWarning("Could not extract date from any header for email Subject={Subject}, using default date",
                message.Subject);
            
            return DateTimeOffset.MinValue;
        }

        /// <summary>
        /// Extracts a date from a Received header value
        /// </summary>
        /// <param name="receivedHeader">The Received header value</param>
        /// <returns>A DateTimeOffset if parsing was successful, null otherwise</returns>
        private DateTimeOffset? ExtractDateFromReceivedHeader(string receivedHeader)
        {
            if (string.IsNullOrEmpty(receivedHeader))
                return null;

            // Received headers typically end with a date in format like:
            // ; Sat, 16 Dec 2000 08:45:05 +0100 (CET)
            // Find the semicolon that precedes the date
            var lastSemicolon = receivedHeader.LastIndexOf(';');
            if (lastSemicolon < 0 || lastSemicolon >= receivedHeader.Length - 1)
                return null;

            var datePart = receivedHeader.Substring(lastSemicolon + 1).Trim();

            // Try to parse the date part
            return ParseDateHeaderValue(datePart);
        }

        /// <summary>
        /// Parses a date string from a header value, handling various formats gracefully
        /// </summary>
        /// <param name="dateString">The date string to parse</param>
        /// <returns>A DateTimeOffset if parsing was successful, null otherwise</returns>
        private DateTimeOffset? ParseDateHeaderValue(string dateString)
        {
            if (string.IsNullOrEmpty(dateString))
                return null;

            // Remove any trailing comments in parentheses like (CET) or (GMT)
            var parenIndex = dateString.IndexOf('(');
            if (parenIndex > 0)
            {
                dateString = dateString.Substring(0, parenIndex).Trim();
            }

            // Try various date formats
            var formats = new[]
            {
                "ddd, d MMM yyyy H:mm:ss zzz",
                "ddd, d MMM yyyy HH:mm:ss zzz",
                "ddd, d MMM yyyy H:mm:ss",
                "ddd, d MMM yyyy HH:mm:ss",
                "d MMM yyyy H:mm:ss zzz",
                "d MMM yyyy HH:mm:ss zzz",
                "d MMM yyyy H:mm:ss",
                "d MMM yyyy HH:mm:ss",
                "ddd, d MMM yy H:mm:ss zzz",
                "ddd, d MMM yy HH:mm:ss zzz",
                "d MMM yy H:mm:ss zzz",
                "d MMM yy HH:mm:ss zzz"
            };

            foreach (var format in formats)
            {
                if (DateTimeOffset.TryParseExact(dateString, format, CultureInfo.InvariantCulture, 
                    DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal, out var result))
                {
                    return result;
                }
            }

            // Try the standard RFC 2822 date parsing as a fallback
            if (DateTimeOffset.TryParse(dateString, CultureInfo.InvariantCulture, 
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal, out var parsedDate))
            {
                return parsedDate;
            }

            return null;
        }

        #endregion

        #region Text

        private string CleanText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            // Remove null characters
            text = text.Replace("\0", "");

            // Use a more encoding-safe approach to remove control characters
            // Only remove control characters except for common whitespace characters
            // This preserves extended ASCII and Unicode characters
            var cleanedText = new StringBuilder(text.Length);
            foreach (var c in text)
            {
                // Keep common whitespace characters and anything above the control character range
                if (c == '\r' || c == '\n' || c == '\t' || c >= 32)
                {
                    cleanedText.Append(c);
                }
                else
                {
                    // Replace other control characters with space
                    cleanedText.Append(' ');
                }
            }

            return cleanedText.ToString();
        }

        private string CleanHtmlForStorage(string html)
        {
            if (string.IsNullOrEmpty(html))
                return string.Empty;

            // Remove null characters efficiently - only if they exist
            if (html.Contains('\0'))
            {
                html = html.Replace("\0", "");
            }

            // Early return for small HTML content
            if (html.Length <= MaxHtmlSizeBytes)
                return html;

            // Calculate safe truncation position
            int maxContentSize = MaxHtmlSizeBytes - TruncationOverhead;
            if (maxContentSize <= 0)
            {
                // Fallback for edge case - return minimal valid HTML
                return $"<html><body>{TruncationNotice}</body></html>";
            }

            int truncatePosition = Math.Min(maxContentSize, html.Length);

            // IMPORTANT: Preserve inline images with cid: references
            // Find all <img> tags with cid: references and ensure they're included
            var imgMatches = Regex.Matches(html, @"<img[^>]*src\s*=\s*[""']cid:[^""']+[""'][^>]*>", RegexOptions.IgnoreCase);

            // Find the last complete <img> tag with cid: before truncation point
            int lastCompleteImgEnd = -1;
            foreach (Match match in imgMatches)
            {
                int imgEnd = match.Index + match.Length;
                if (imgEnd <= truncatePosition)
                {
                    lastCompleteImgEnd = imgEnd;
                }
                else
                {
                    // This image would be cut off, adjust truncation point if possible
                    if (match.Index < truncatePosition && match.Index > maxContentSize / 2)
                    {
                        // If the image starts before truncation but ends after, try to truncate before it
                        truncatePosition = match.Index;
                        _logger.LogDebug("Adjusted truncation to preserve inline images, truncating before <img> tag at position {Position}", match.Index);
                    }
                    break;
                }
            }

            // Find safe truncation point that doesn't break HTML tags
            int lastLessThan = html.LastIndexOf('<', truncatePosition - 1);
            int lastGreaterThan = html.LastIndexOf('>', truncatePosition - 1);

            // If we're inside a tag, truncate before it starts
            if (lastLessThan > lastGreaterThan && lastLessThan >= 0)
            {
                truncatePosition = lastLessThan;
            }
            else if (lastGreaterThan >= 0)
            {
                // Otherwise, truncate after the last complete tag
                truncatePosition = lastGreaterThan + 1;
            }

            // Use StringBuilder for efficient string building
            var result = new StringBuilder(truncatePosition + TruncationNotice.Length + 50);

            // Get base content as span for better performance
            ReadOnlySpan<char> baseContent = html.AsSpan(0, truncatePosition);

            // Check for HTML structure efficiently
            bool hasHtml = baseContent.Contains("<html".AsSpan(), StringComparison.OrdinalIgnoreCase);
            bool hasBody = baseContent.Contains("<body".AsSpan(), StringComparison.OrdinalIgnoreCase);

            // Build the result efficiently
            if (!hasHtml)
            {
                result.Append("<html>");
            }

            if (!hasBody)
            {
                if (hasHtml)
                {
                    // Find where to insert <body> tag efficiently
                    string contentStr = baseContent.ToString();
                    int htmlStart = contentStr.IndexOf("<html", StringComparison.OrdinalIgnoreCase);
                    if (htmlStart >= 0)
                    {
                        int htmlTagEnd = contentStr.IndexOf('>', htmlStart);
                        if (htmlTagEnd >= 0)
                        {
                            result.Append(baseContent.Slice(0, htmlTagEnd + 1));
                            result.Append("<body>");
                            result.Append(baseContent.Slice(htmlTagEnd + 1));
                        }
                        else
                        {
                            result.Append("<body>");
                            result.Append(baseContent);
                        }
                    }
                    else
                    {
                        result.Append("<body>");
                        result.Append(baseContent);
                    }
                }
                else
                {
                    result.Append("<body>");
                    result.Append(baseContent);
                }
            }
            else
            {
                result.Append(baseContent);
            }

            // Add truncation notice
            result.Append(TruncationNotice);

            // Close tags efficiently
            string resultStr = result.ToString();
            if (!resultStr.EndsWith("</body>", StringComparison.OrdinalIgnoreCase))
            {
                result.Append("</body>");
            }
            if (!resultStr.EndsWith("</html>", StringComparison.OrdinalIgnoreCase))
            {
                result.Append("</html>");
            }

            return result.ToString();
        }

        /// <summary>
        /// Truncates a single field to ensure it doesn't exceed tsvector limits
        /// </summary>
        /// <param name="text">The field text to truncate</param>
        /// <param name="maxSizeBytes">Maximum size in bytes</param>
        /// <returns>Truncated text</returns>
        private string TruncateFieldForTsvector(string text, int maxSizeBytes)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            // Check if truncation is needed
            if (Encoding.UTF8.GetByteCount(text) <= maxSizeBytes)
            {
                return text; // No truncation needed
            }

            // Find a safe truncation point
            int approximateCharPosition = Math.Min(maxSizeBytes, text.Length);

            // Work backwards to ensure we don't exceed byte limit
            while (approximateCharPosition > 0 && Encoding.UTF8.GetByteCount(text.Substring(0, approximateCharPosition)) > maxSizeBytes)
            {
                approximateCharPosition--;
            }

            // Try to find a word boundary to avoid breaking in the middle of a word
            int wordBoundarySearch = Math.Max(0, approximateCharPosition - 50);
            int lastSpaceIndex = text.LastIndexOf(' ', approximateCharPosition - 1, approximateCharPosition - wordBoundarySearch);

            if (lastSpaceIndex > wordBoundarySearch)
            {
                approximateCharPosition = lastSpaceIndex;
            }

            return text.Substring(0, approximateCharPosition) + "...";
        }

        /// <summary>
        /// Truncates text content to fit within tsvector size limits while preserving readability
        /// </summary>
        /// <param name="text">The text to truncate</param>
        /// <param name="maxSizeBytes">Maximum size in bytes</param>
        /// <returns>Truncated text with notice appended</returns>
        private string TruncateTextForStorage(string text, int maxSizeBytes)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            const string textTruncationNotice = "\n\n[CONTENT TRUNCATED - This email contains very large text content that has been truncated for better performance. The complete original content has been saved as an attachment.]";

            // Calculate overhead for the truncation notice
            int noticeOverhead = Encoding.UTF8.GetByteCount(textTruncationNotice);
            int maxContentSize = maxSizeBytes - noticeOverhead;

            if (maxContentSize <= 0)
            {
                // Edge case - just return the notice
                return textTruncationNotice;
            }

            // Check if we need to truncate
            if (Encoding.UTF8.GetByteCount(text) <= maxSizeBytes)
            {
                return text; // No truncation needed
            }

            // Find a safe truncation point that doesn't break in the middle of a word
            int approximateCharPosition = Math.Min(maxContentSize, text.Length);

            // Work backwards to find a word boundary or reasonable break point
            while (approximateCharPosition > 0 && Encoding.UTF8.GetByteCount(text.Substring(0, approximateCharPosition)) > maxContentSize)
            {
                approximateCharPosition--;
            }

            // Try to find a word boundary within the last 100 characters to avoid breaking words
            int wordBoundarySearch = Math.Max(0, approximateCharPosition - 100);
            int lastSpaceIndex = text.LastIndexOf(' ', approximateCharPosition - 1, approximateCharPosition - wordBoundarySearch);
            int lastNewlineIndex = text.LastIndexOf('\n', approximateCharPosition - 1, approximateCharPosition - wordBoundarySearch);
            int lastPunctuationIndex = text.LastIndexOfAny(new char[] { '.', '!', '?', ';' }, approximateCharPosition - 1, approximateCharPosition - wordBoundarySearch);

            // Use the best break point found
            int breakPoint = Math.Max(Math.Max(lastSpaceIndex, lastNewlineIndex), lastPunctuationIndex);
            if (breakPoint > wordBoundarySearch)
            {
                approximateCharPosition = breakPoint + 1; // Include the break character
            }

            // Final safety check to ensure we don't exceed byte limit
            string truncatedContent = text.Substring(0, approximateCharPosition);
            while (Encoding.UTF8.GetByteCount(truncatedContent + textTruncationNotice) > maxSizeBytes && truncatedContent.Length > 0)
            {
                truncatedContent = truncatedContent.Substring(0, truncatedContent.Length - 1);
            }

            return truncatedContent + textTruncationNotice;
        }

        /// <summary>
        /// Checks if a folder name indicates outgoing mail based on its name in multiple languages
        /// </summary>
        /// <param name="folderName">The folder name to check</param>
        /// <returns>True if the folder name indicates outgoing mail, false otherwise</returns>
        public bool IsOutgoingFolderByName(string folderName)
        {
            var outgoingFolderNames = new[]
            {
                // Arabic
                "المرسلة", "البريد المرسل",

                // Bulgarian
                "изпратени", "изпратена поща",

                // Chinese (Simplified)
                "已发送", "已传送",

                // Croatian
                "poslano", "poslana pošta",

                // Czech
                "odeslané", "odeslaná pošta",

                // Danish
                "sendt", "sendte elementer",

                // Dutch
                "verzonden", "verzonden items", "verzonden e-mail",

                // English
                "sent", "sent items", "sent mail",

                // Estonian
                "saadetud", "saadetud kirjad",

                // Finnish
                "lähetetyt", "lähetetyt kohteet",

                // French
                "envoyé", "éléments envoyés", "mail envoyé",

                // German
                "gesendet", "gesendete objekte", "gesendete",

                // Greek
                "απεσταλμένα", "σταλμένα", "σταλμένα μηνύματα",

                // Hebrew
                "נשלחו", "דואר יוצא",

                // Hungarian
                "elküldött", "elküldött elemek",

                // Irish
                "seolta", "r-phost seolta",

                // Italian
                "inviato", "posta inviata", "elementi inviati",

                // Japanese
                "送信済み", "送信済メール", "送信メール",

                // Korean
                "보낸편지함", "발신함", "보낸메일",

                // Latvian
                "nosūtītie", "nosūtītās vēstules",

                // Lithuanian
                "išsiųsta", "išsiųsti laiškai",

                // Maltese
                "mibgħuta", "posta mibgħuta",

                // Norwegian
                "sendt", "sendte elementer",

                // Polish
                "wysłane", "elementy wysłane",

                // Portuguese
                "enviados", "itens enviados", "mensagens enviadas",

                // Romanian
                "trimise", "elemente trimise", "mail trimis",

                // Russian
                "отправленные", "исходящие", "отправлено",

                // Slovak
                "odoslané", "odoslaná pošta",

                // Slovenian
                "poslano", "poslana pošta",

                // Spanish
                "enviado", "elementos enviados", "correo enviado",

                // Swedish
                "skickat", "skickade objekt",

                // Turkish
                "gönderilen", "gönderilmiş öğeler"
            };

            string folderNameLower = folderName?.ToLowerInvariant() ?? "";
            return outgoingFolderNames.Any(name => folderNameLower.Contains(name));
        }

        private bool IsDraftsFolder(string folderName)
        {
            var draftsFolderNames = new[]
            {
                "drafts", "entwürfe", "brouillons", "bozze"
            };

            string folderNameLower = folderName?.ToLowerInvariant() ?? "";
            return draftsFolderNames.Any(name => folderNameLower.Contains(name));
        }

        #endregion

        #region Local Retention

        /// <summary>
        /// Deletes old emails from the local archive based on LocalRetentionDays setting
        /// </summary>
        public async Task<int> DeleteOldLocalEmailsAsync(MailAccount account, string? jobId = null)
        {
            if (!account.LocalRetentionDays.HasValue || account.LocalRetentionDays.Value <= 0)
            {
                return 0;
            }

            var cutoffDate = DateTime.UtcNow.AddDays(-account.LocalRetentionDays.Value);

            _logger.LogInformation("Starting deletion of local archived emails older than {Days} days (before {CutoffDate}) for account {AccountName}",
                account.LocalRetentionDays.Value, cutoffDate, account.Name);

            try
            {
                // Find all emails older than the cutoff date for this account
                var emailsToDelete = await _context.ArchivedEmails
                    .Where(e => e.MailAccountId == account.Id && e.SentDate < cutoffDate)
                    .Select(e => new { e.Id, e.Subject, e.From, e.SentDate })
                    .ToListAsync();

                if (emailsToDelete.Count == 0)
                {
                    _logger.LogInformation("No emails found to delete from local archive for account {AccountName}", account.Name);
                    return 0;
                }

                _logger.LogInformation("Found {Count} emails to delete from local archive for account {AccountName}",
                    emailsToDelete.Count, account.Name);

                // Delete in batches to avoid memory issues
                var batchSize = _batchOptions.BatchSize;
                var deletedCount = 0;

                for (int i = 0; i < emailsToDelete.Count; i += batchSize)
                {
                    var batch = emailsToDelete.Skip(i).Take(batchSize).ToList();
                    var batchIds = batch.Select(e => e.Id).ToList();

                    try
                    {
                        // Delete attachments first (cascade delete should handle this, but being explicit)
                        var attachmentsDeleted = await _context.EmailAttachments
                            .Where(a => batchIds.Contains(a.ArchivedEmailId))
                            .ExecuteDeleteAsync();

                        // Delete emails
                        var emailsDeleted = await _context.ArchivedEmails
                            .Where(e => batchIds.Contains(e.Id))
                            .ExecuteDeleteAsync();

                        deletedCount += emailsDeleted;

                        _logger.LogDebug("Deleted batch of {Count} emails ({Attachments} attachments) from local archive",
                            emailsDeleted, attachmentsDeleted);

                        await _context.SaveChangesAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error deleting batch from local archive for account {AccountName}: {Message}",
                            account.Name, ex.Message);
                    }

                    // Add a small delay between batches
                    if (i + batchSize < emailsToDelete.Count && _batchOptions.PauseBetweenBatchesMs > 0)
                    {
                        await Task.Delay(_batchOptions.PauseBetweenBatchesMs);
                    }
                }

                _logger.LogInformation("Completed local archive deletion for account {AccountName}. Deleted {Count} emails",
                    account.Name, deletedCount);

                // Log the deletion summary to AccessLogs
                if (deletedCount > 0)
                {
                    try
                    {
                        var accessLog = new AccessLog
                        {
                            Username = "System",
                            Type = AccessLogType.Deletion,
                            Timestamp = DateTime.UtcNow,
                            SearchParameters = $"Local retention: Deleted {deletedCount} emails older than {account.LocalRetentionDays} days from local archive",
                            MailAccountId = account.Id
                        };

                        _context.AccessLogs.Add(accessLog);
                        await _context.SaveChangesAsync();
                    }
                    catch (Exception logEx)
                    {
                        _logger.LogWarning(logEx, "Failed to log local retention deletion summary to AccessLogs");
                    }
                }

                return deletedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during local archive deletion for account {AccountName}: {Message}",
                    account.Name, ex.Message);
                return 0;
            }
        }

        #endregion
    }
}

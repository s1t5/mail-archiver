using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Text.RegularExpressions;

namespace MailArchiver.Services
{
    public class EmailSearchService
    {
        private readonly MailArchiverDbContext _context;
        private readonly ILogger<EmailSearchService> _logger;

        public EmailSearchService(
            MailArchiverDbContext context,
            ILogger<EmailSearchService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Optimized search that directly uses the GIN index created in migration
        /// </summary>
        public async Task<(List<ArchivedEmail> Emails, int TotalCount)> SearchEmailsOptimizedAsync(
            string searchTerm,
            DateTime? fromDate,
            DateTime? toDate,
            int? accountId,
            bool? isOutgoing,
            int skip,
            int take,
            List<int> allowedAccountIds = null)
        {
            var startTime = DateTime.UtcNow;
            
            // Validate pagination parameters
            if (take > 1000) take = 1000;
            if (skip < 0) skip = 0;

            try
            {
                // Build the raw SQL query that directly uses the GIN index
                var whereConditions = new List<string>();
                var parameters = new List<NpgsqlParameter>();
                var paramCounter = 0;

                // Full-text search condition (this will use the GIN index)
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    var sanitizedSearchTerm = SanitizeSearchTermForTsQuery(searchTerm);
                    if (!string.IsNullOrEmpty(sanitizedSearchTerm))
                    {
                        whereConditions.Add($@"
                            to_tsvector('simple', 
                                COALESCE(""Subject"", '') || ' ' || 
                                COALESCE(""Body"", '') || ' ' || 
                                COALESCE(""From"", '') || ' ' || 
                                COALESCE(""To"", '') || ' ' || 
                                COALESCE(""Cc"", '') || ' ' || 
                                COALESCE(""Bcc"", '')) 
                            @@ to_tsquery('simple', ${paramCounter})");
                        parameters.Add(new NpgsqlParameter($"${paramCounter}", sanitizedSearchTerm));
                        paramCounter++;
                    }
                }

                // Account filtering
                if (allowedAccountIds != null)
                {
                    if (allowedAccountIds.Any())
                    {
                        whereConditions.Add($@"""MailAccountId"" = ANY(${paramCounter})");
                        parameters.Add(new NpgsqlParameter($"${paramCounter}", allowedAccountIds.ToArray()));
                        paramCounter++;
                    }
                    else
                    {
                        // User has no access to any accounts
                        return (new List<ArchivedEmail>(), 0);
                    }
                }
                else if (accountId.HasValue)
                {
                    whereConditions.Add($@"""MailAccountId"" = ${paramCounter}");
                    parameters.Add(new NpgsqlParameter($"${paramCounter}", accountId.Value));
                    paramCounter++;
                }

                // Date filtering (these will use the composite indexes)
                if (fromDate.HasValue)
                {
                    whereConditions.Add($@"""SentDate"" >= ${paramCounter}");
                    parameters.Add(new NpgsqlParameter($"${paramCounter}", fromDate.Value));
                    paramCounter++;
                }

                if (toDate.HasValue)
                {
                    var endDate = toDate.Value.AddDays(1).AddSeconds(-1);
                    whereConditions.Add($@"""SentDate"" <= ${paramCounter}");
                    parameters.Add(new NpgsqlParameter($"${paramCounter}", endDate));
                    paramCounter++;
                }

                if (isOutgoing.HasValue)
                {
                    whereConditions.Add($@"""IsOutgoing"" = ${paramCounter}");
                    parameters.Add(new NpgsqlParameter($"${paramCounter}", isOutgoing.Value));
                    paramCounter++;
                }

                var whereClause = whereConditions.Any() ? "WHERE " + string.Join(" AND ", whereConditions) : "";

                // Count query (optimized)
                var countSql = $@"
                    SELECT COUNT(*)
                    FROM mail_archiver.""ArchivedEmails""
                    {whereClause}";

                var countStartTime = DateTime.UtcNow;
                var totalCount = await ExecuteScalarQueryAsync<int>(countSql, parameters);
                var countDuration = DateTime.UtcNow - countStartTime;
                _logger.LogInformation("Optimized count query took {Duration}ms for {Count} matching records", 
                    countDuration.TotalMilliseconds, totalCount);

                // Data query (optimized)
                var dataSql = $@"
                    SELECT e.""Id"", e.""MailAccountId"", e.""MessageId"", e.""Subject"", e.""Body"", e.""HtmlBody"",
                           e.""From"", e.""To"", e.""Cc"", e.""Bcc"", e.""SentDate"", e.""ReceivedDate"",
                           e.""IsOutgoing"", e.""HasAttachments"", e.""FolderName"",
                           ma.""Id"" as ""AccountId"", ma.""Name"" as ""AccountName"", ma.""EmailAddress"" as ""AccountEmail""
                    FROM mail_archiver.""ArchivedEmails"" e
                    INNER JOIN mail_archiver.""MailAccounts"" ma ON e.""MailAccountId"" = ma.""Id""
                    {whereClause}
                    ORDER BY e.""SentDate"" DESC
                    LIMIT {take} OFFSET {skip}";

                var dataStartTime = DateTime.UtcNow;
                var emails = await ExecuteDataQueryAsync(dataSql, parameters);
                var dataDuration = DateTime.UtcNow - dataStartTime;
                _logger.LogInformation("Optimized data query took {Duration}ms for {Count} records", 
                    dataDuration.TotalMilliseconds, emails.Count);

                var totalDuration = DateTime.UtcNow - startTime;
                _logger.LogInformation("Total optimized search operation took {Duration}ms", totalDuration.TotalMilliseconds);

                return (emails, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in optimized search: {Message}", ex.Message);
                
                // Fallback to the original EF method
                _logger.LogWarning("Falling back to Entity Framework search");
                return await FallbackToEFSearchAsync(searchTerm, fromDate, toDate, accountId, isOutgoing, skip, take, allowedAccountIds);
            }
        }

        private async Task<T> ExecuteScalarQueryAsync<T>(string sql, List<NpgsqlParameter> parameters)
        {
            using var connection = new NpgsqlConnection(_context.Database.GetConnectionString());
            await connection.OpenAsync();
            
            using var command = new NpgsqlCommand(sql, connection);
            foreach (var parameter in parameters)
            {
                command.Parameters.Add(parameter);
            }
            
            var result = await command.ExecuteScalarAsync();
            return (T)Convert.ChangeType(result, typeof(T));
        }

        private async Task<List<ArchivedEmail>> ExecuteDataQueryAsync(string sql, List<NpgsqlParameter> parameters)
        {
            var emails = new List<ArchivedEmail>();
            
            using var connection = new NpgsqlConnection(_context.Database.GetConnectionString());
            await connection.OpenAsync();
            
            using var command = new NpgsqlCommand(sql, connection);
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

        private string SanitizeSearchTermForTsQuery(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return null;

            _logger.LogDebug("Sanitizing search term for tsquery: '{SearchTerm}'", searchTerm);

            // Remove special PostgreSQL tsquery operators and characters that could break the query
            var sanitized = Regex.Replace(searchTerm, @"[&|!():\*]", " ", RegexOptions.None);
            
            // Remove extra whitespace
            sanitized = Regex.Replace(sanitized, @"\s+", " ", RegexOptions.None).Trim();
            
            if (string.IsNullOrEmpty(sanitized))
                return null;

            // Split into terms and join with & (AND operator)
            var terms = sanitized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (!terms.Any())
                return null;

            // Escape single quotes and join with AND
            var escapedTerms = terms.Select(t => t.Replace("'", "''"));
            var result = string.Join(" & ", escapedTerms);
            
            _logger.LogDebug("Sanitized search term result: '{Result}'", result);
            return result;
        }

        private async Task<(List<ArchivedEmail> Emails, int TotalCount)> FallbackToEFSearchAsync(
            string searchTerm,
            DateTime? fromDate,
            DateTime? toDate,
            int? accountId,
            bool? isOutgoing,
            int skip,
            int take,
            List<int> allowedAccountIds = null)
        {
            // This is the original Entity Framework implementation as fallback
            var baseQuery = _context.ArchivedEmails.AsNoTracking().AsQueryable();

            if (allowedAccountIds != null)
            {
                if (allowedAccountIds.Any())
                {
                    baseQuery = baseQuery.Where(e => allowedAccountIds.Contains(e.MailAccountId));
                }
                else
                {
                    baseQuery = baseQuery.Where(e => false);
                }
            }

            if (accountId.HasValue)
            {
                baseQuery = baseQuery.Where(e => e.MailAccountId == accountId.Value);
            }

            if (fromDate.HasValue)
                baseQuery = baseQuery.Where(e => e.SentDate >= fromDate.Value);

            if (toDate.HasValue)
                baseQuery = baseQuery.Where(e => e.SentDate <= toDate.Value.AddDays(1).AddSeconds(-1));

            if (isOutgoing.HasValue)
                baseQuery = baseQuery.Where(e => e.IsOutgoing == isOutgoing.Value);

            IQueryable<ArchivedEmail> searchQuery = baseQuery;
            if (!string.IsNullOrEmpty(searchTerm))
            {
                var escapedSearchTerm = searchTerm.Replace("'", "''");
                searchQuery = baseQuery.Where(e =>
                    EF.Functions.ILike(e.Subject, $"%{escapedSearchTerm}%") ||
                    EF.Functions.ILike(e.From, $"%{escapedSearchTerm}%") ||
                    EF.Functions.ILike(e.To, $"%{escapedSearchTerm}%") ||
                    EF.Functions.ILike(e.Body, $"%{escapedSearchTerm}%")
                );
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
    }
}

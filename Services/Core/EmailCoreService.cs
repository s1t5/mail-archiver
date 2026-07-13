using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Models.ViewModels;
using MailArchiver.Services.Shared;
using MailArchiver.Utilities;
using MailArchiver.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MimeKit;
using System.Globalization;
using System.Text;
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

        // Command timeout (seconds) for the raw ADO.NET search queries. Honours the configured
        // Npgsql:CommandTimeout (the same key the EF context uses) but enforces a floor so long
        // full-text searches are not aborted by the Npgsql default of 30s.
        private readonly int _searchCommandTimeoutSeconds;

        public EmailCoreService(
            MailArchiverDbContext context,
            ILogger<EmailCoreService> logger,
            DateTimeHelper dateTimeHelper,
            IOptions<BatchOperationOptions> batchOptions,
            IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _dateTimeHelper = dateTimeHelper;
            _batchOptions = batchOptions.Value;
            var configuredTimeout = configuration.GetValue<int>("Npgsql:CommandTimeout", 60);
            // 0 = Npgsql's "no timeout"; honour it. Otherwise enforce a floor so the raw search
            // commands are not aborted by the 30s default.
            _searchCommandTimeoutSeconds = configuredTimeout == 0 ? 0 : Math.Max(configuredTimeout, 120);
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

        internal async Task<(List<ArchivedEmail> Emails, int TotalCount)> SearchEmailsOptimizedAsync(
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

            // Full-text search condition: AND of OR-groups of typed clauses.
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var groups = ParseSearchClauses(searchTerm);
                var searchConditions = new List<string>();

                if (groups.Count > 0 && groups.All(g => g.All(c => c.Kind == ClauseKind.Word)))
                {
                    if (groups.All(g => g.All(c => c.Negated)))
                    {
                        // Pure-negation ("exclude only"): a flat NOT(tsv @@ q) seq-scans and re-tokenizes
                        // every row's body (~minutes on large archives). Rewrite via De Morgan to an
                        // index-accelerated positive set and filter by anti-membership on the primary key.
                        searchConditions.Add($@"e.""Id"" NOT IN (SELECT ""Id"" FROM mail_archiver.""ArchivedEmails"" WHERE {FtsExpr} @@ to_tsquery('simple', @param{paramCounter}))");
                        parameters.Add(new Npgsql.NpgsqlParameter($"@param{paramCounter}", BuildNegationComplementTsQuery(groups)));
                        paramCounter++;
                    }
                    else
                    {
                        // Fast path: a pure-word query -> one combined tsquery (single GIN scan).
                        searchConditions.Add($"{FtsExpr} @@ to_tsquery('simple', @param{paramCounter})");
                        parameters.Add(new Npgsql.NpgsqlParameter($"@param{paramCounter}", BuildWordTsQuery(groups)));
                        paramCounter++;
                    }
                }
                else
                {
                    // Mixed query: each OR-group becomes (clause OR clause ...); groups are ANDed.
                    foreach (var group in groups)
                    {
                        var conds = new List<string>();
                        foreach (var clause in group)
                        {
                            string cond;
                            if (clause.Kind == ClauseKind.Word)
                            {
                                cond = $"{FtsExpr} @@ to_tsquery('simple', @param{paramCounter})";
                                parameters.Add(new Npgsql.NpgsqlParameter($"@param{paramCounter}", WordAtom(clause)));
                                paramCounter++;
                            }
                            else if (clause.Kind == ClauseKind.Substring)
                            {
                                cond = $"{LowerConcatExpr} LIKE '%' || lower(@param{paramCounter}) || '%' ESCAPE '\\'";
                                parameters.Add(new Npgsql.NpgsqlParameter($"@param{paramCounter}", EscapeLike(clause.Text)));
                                paramCounter++;
                                if (clause.Negated) cond = $"NOT ({cond})";
                            }
                            else if (clause.Kind == ClauseKind.Field)
                            {
                                cond = $"POSITION(LOWER(@param{paramCounter}) IN LOWER(COALESCE(\"{clause.Column}\", ''))) > 0";
                                parameters.Add(new Npgsql.NpgsqlParameter($"@param{paramCounter}", clause.Text));
                                paramCounter++;
                                if (clause.Negated) cond = $"NOT ({cond})";
                            }
                            else if (clause.Kind == ClauseKind.Attachment)
                            {
                                cond = clause.Negated ? "\"HasAttachments\" = FALSE" : "\"HasAttachments\" = TRUE";
                            }
                            else // Phrase: GIN @@ prefilter narrows rows, POSITION confirms the exact phrase.
                            {
                                var phraseTs = BuildPhraseTsQuery(clause.Text);
                                if (!string.IsNullOrEmpty(phraseTs))
                                {
                                    cond = $@"({FtsExpr} @@ to_tsquery('simple', @param{paramCounter}) AND (POSITION(LOWER(@param{paramCounter + 1}) IN LOWER(COALESCE(""Subject"", ''))) > 0 OR POSITION(LOWER(@param{paramCounter + 1}) IN LOWER(COALESCE(""Body"", ''))) > 0 OR POSITION(LOWER(@param{paramCounter + 1}) IN LOWER(COALESCE(""From"", ''))) > 0 OR POSITION(LOWER(@param{paramCounter + 1}) IN LOWER(COALESCE(""To"", ''))) > 0 OR POSITION(LOWER(@param{paramCounter + 1}) IN LOWER(COALESCE(""Cc"", ''))) > 0 OR POSITION(LOWER(@param{paramCounter + 1}) IN LOWER(COALESCE(""Bcc"", ''))) > 0))";
                                    parameters.Add(new Npgsql.NpgsqlParameter($"@param{paramCounter}", phraseTs));
                                    paramCounter++;
                                    parameters.Add(new Npgsql.NpgsqlParameter($"@param{paramCounter}", clause.Text));
                                    paramCounter++;
                                }
                                else
                                {
                                    cond = $@"(POSITION(LOWER(@param{paramCounter}) IN LOWER(COALESCE(""Subject"", ''))) > 0 OR POSITION(LOWER(@param{paramCounter}) IN LOWER(COALESCE(""Body"", ''))) > 0 OR POSITION(LOWER(@param{paramCounter}) IN LOWER(COALESCE(""From"", ''))) > 0 OR POSITION(LOWER(@param{paramCounter}) IN LOWER(COALESCE(""To"", ''))) > 0 OR POSITION(LOWER(@param{paramCounter}) IN LOWER(COALESCE(""Cc"", ''))) > 0 OR POSITION(LOWER(@param{paramCounter}) IN LOWER(COALESCE(""Bcc"", ''))) > 0)";
                                    parameters.Add(new Npgsql.NpgsqlParameter($"@param{paramCounter}", clause.Text));
                                    paramCounter++;
                                }
                                if (clause.Negated) cond = $"NOT ({cond})";
                            }
                            conds.Add(cond);
                        }
                        if (conds.Count > 0)
                            searchConditions.Add(conds.Count == 1 ? conds[0] : "(" + string.Join(" OR ", conds) + ")");
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
                if (allowedAccountIds != null && !allowedAccountIds.Contains(accountId.Value))
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
                FROM mail_archiver.""ArchivedEmails"" e
                {whereClause}";

            var totalCount = await ExecuteScalarQueryAsync<int>(countSql, CloneParameters(parameters));

            // Build ORDER BY clause
            var (orderByClause, sortColumn, isTimestampSort) = GetOrderByClause(sortBy, sortOrder);

            // For timestamp sorts (SentDate/ReceivedDate), use a MATERIALIZED CTE to force the planner
            // to use the GIN full-text index for the FTS predicate instead of doing a backward SentDate
            // index scan with per-row re-tokenization of the body. The CTE selects only (Id, SortColumn)
            // so no body detoasting happens during matching; the body is only detoasted for the final page.
            // For text sorts, keep the flat form (no backward-index-scan antipattern there).
            string dataSql;
            if (isTimestampSort)
            {
                dataSql = $@"
                    WITH ""matched"" AS MATERIALIZED (
                        SELECT e.""Id"", e.""{sortColumn}""
                        FROM mail_archiver.""ArchivedEmails"" e
                        {whereClause}
                    ),
                    ""page"" AS (
                        SELECT m.""Id"", m.""{sortColumn}""
                        FROM ""matched"" m
                        ORDER BY m.""{sortColumn}"" {(sortOrder?.ToLower() == "asc" ? "ASC" : "DESC")}
                        LIMIT {take} OFFSET {skip}
                    )
                    SELECT e.""Id"", e.""MailAccountId"", e.""MessageId"", e.""Subject"", e.""Body"", e.""HtmlBody"",
                           e.""From"", e.""To"", e.""Cc"", e.""Bcc"", e.""SentDate"", e.""ReceivedDate"",
                           e.""IsOutgoing"", e.""HasAttachments"", e.""FolderName"", e.""IsLocked"",
                           ma.""Id"" as ""AccountId"", ma.""Name"" as ""AccountName"", ma.""EmailAddress"" as ""AccountEmail""
                    FROM ""page"" p
                    INNER JOIN mail_archiver.""ArchivedEmails"" e ON e.""Id"" = p.""Id""
                    INNER JOIN mail_archiver.""MailAccounts"" ma ON e.""MailAccountId"" = ma.""Id""
                    ORDER BY p.""{sortColumn}"" {(sortOrder?.ToLower() == "asc" ? "ASC" : "DESC")}";
            }
            else
            {
                dataSql = $@"
                    SELECT e.""Id"", e.""MailAccountId"", e.""MessageId"", e.""Subject"", e.""Body"", e.""HtmlBody"",
                           e.""From"", e.""To"", e.""Cc"", e.""Bcc"", e.""SentDate"", e.""ReceivedDate"",
                           e.""IsOutgoing"", e.""HasAttachments"", e.""FolderName"", e.""IsLocked"",
                           ma.""Id"" as ""AccountId"", ma.""Name"" as ""AccountName"", ma.""EmailAddress"" as ""AccountEmail""
                    FROM mail_archiver.""ArchivedEmails"" e
                    INNER JOIN mail_archiver.""MailAccounts"" ma ON e.""MailAccountId"" = ma.""Id""
                    {whereClause}
                    {orderByClause}
                    LIMIT {take} OFFSET {skip}";
            }

            var emails = await ExecuteDataQueryAsync(dataSql, CloneParameters(parameters));

            return (emails, totalCount);
        }

        private async Task<T> ExecuteScalarQueryAsync<T>(string sql, List<Npgsql.NpgsqlParameter> parameters)
        {
            using var connection = new Npgsql.NpgsqlConnection(_context.Database.GetConnectionString());
            await connection.OpenAsync();

            using var command = new Npgsql.NpgsqlCommand(sql, connection);
            command.CommandTimeout = _searchCommandTimeoutSeconds;
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
            command.CommandTimeout = _searchCommandTimeoutSeconds;
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

        // Fallback helpers: build a composable "any searched field ILIKE %term%" predicate so the
        // EF fallback can preserve OR-groups (OR within a group, AND across groups).
        // Escapes LIKE/ILIKE metacharacters so *term* / field terms match them literally (ESCAPE '\').
        private static string EscapeLike(string s) => s.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

        private static System.Linq.Expressions.Expression<Func<ArchivedEmail, bool>> FieldContainsPredicate(string term)
        {
            var pattern = "%" + EscapeLike(term) + "%";
            return e => EF.Functions.ILike(e.Subject, pattern, "\\") || EF.Functions.ILike(e.From, pattern, "\\") ||
                        EF.Functions.ILike(e.To, pattern, "\\") || EF.Functions.ILike(e.Body, pattern, "\\") ||
                        EF.Functions.ILike(e.Cc, pattern, "\\") || EF.Functions.ILike(e.Bcc, pattern, "\\");
        }

        private static System.Linq.Expressions.Expression<Func<T, bool>> OrElsePredicate<T>(
            System.Linq.Expressions.Expression<Func<T, bool>> a,
            System.Linq.Expressions.Expression<Func<T, bool>> b)
        {
            var p = System.Linq.Expressions.Expression.Parameter(typeof(T), "e");
            var body = System.Linq.Expressions.Expression.OrElse(
                new ParameterRebinder(a.Parameters[0], p).Visit(a.Body),
                new ParameterRebinder(b.Parameters[0], p).Visit(b.Body));
            return System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(body, p);
        }

        private static System.Linq.Expressions.Expression<Func<ArchivedEmail, bool>> FieldColumnPredicate(string column, string term)
        {
            var pattern = "%" + EscapeLike(term) + "%";
            return column switch
            {
                "Subject" => e => EF.Functions.ILike(e.Subject, pattern, "\\"),
                "Body" => e => EF.Functions.ILike(e.Body, pattern, "\\"),
                "From" => e => EF.Functions.ILike(e.From, pattern, "\\"),
                "To" => e => EF.Functions.ILike(e.To, pattern, "\\"),
                _ => e => EF.Functions.ILike(e.Subject, pattern, "\\")
            };
        }

        private static bool IsAttachmentKeyword(string v)
        {
            if (string.IsNullOrWhiteSpace(v)) return false;
            switch (v.Trim().ToLowerInvariant())
            {
                case "attachment":
                case "attachments":
                case "anhang":
                case "anhänge":
                case "anhaenge":
                case "file":
                case "files":
                case "yes":
                case "true":
                case "1":
                    return true;
                default:
                    return false;
            }
        }

        private static System.Linq.Expressions.Expression<Func<ArchivedEmail, bool>> ClausePredicate(SearchClause c)
        {
            if (c.Kind == ClauseKind.Attachment)
            {
                System.Linq.Expressions.Expression<Func<ArchivedEmail, bool>> a = e => e.HasAttachments;
                return c.Negated ? NotPredicate(a) : a;
            }
            var p = c.Kind == ClauseKind.Field ? FieldColumnPredicate(c.Column, c.Text) : FieldContainsPredicate(c.Text);
            return c.Negated ? NotPredicate(p) : p;
        }

        private static System.Linq.Expressions.Expression<Func<T, bool>> NotPredicate<T>(
            System.Linq.Expressions.Expression<Func<T, bool>> a)
            => System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(
                System.Linq.Expressions.Expression.Not(a.Body), a.Parameters);

        private sealed class ParameterRebinder : System.Linq.Expressions.ExpressionVisitor
        {
            private readonly System.Linq.Expressions.ParameterExpression _from;
            private readonly System.Linq.Expressions.ParameterExpression _to;
            public ParameterRebinder(System.Linq.Expressions.ParameterExpression from, System.Linq.Expressions.ParameterExpression to)
            {
                _from = from;
                _to = to;
            }
            protected override System.Linq.Expressions.Expression VisitParameter(System.Linq.Expressions.ParameterExpression node)
                => node == _from ? _to : base.VisitParameter(node);
        }

        // ===== Unified search-clause model =====
        // A query is an AND of OR-groups; every operand type (word / phrase / field / substring,
        // each optionally negated) is a typed clause that can be a member of an OR-group, so
        // "from:a OR from:b", "*x* OR *y*" and mixed "invoice OR from:acme" all combine correctly.
        internal enum ClauseKind { Word, Phrase, Field, Substring, Attachment }

        internal readonly struct SearchClause
        {
            public ClauseKind Kind { get; init; }
            public string Text { get; init; }
            public string Column { get; init; } // set for Field clauses
            public bool Negated { get; init; }
        }

        private const int MaxClauseGroups = 256;
        private const string FtsExpr = @"to_tsvector('simple', COALESCE(""Subject"", '') || ' ' || COALESCE(""Body"", '') || ' ' || COALESCE(""From"", '') || ' ' || COALESCE(""To"", '') || ' ' || COALESCE(""Cc"", '') || ' ' || COALESCE(""Bcc"", ''))";
        private const string LowerConcatExpr = @"lower(COALESCE(""Subject"", '') || ' ' || COALESCE(""Body"", '') || ' ' || COALESCE(""From"", '') || ' ' || COALESCE(""To"", '') || ' ' || COALESCE(""Cc"", '') || ' ' || COALESCE(""Bcc"", ''))";

        // Parses the search term into AND-of-OR-groups of typed clauses. OR binds its adjacent
        // neighbours (Google-style); -term / !term negates a word/substring; "phrase"; field syntax
        // subject:/body:/from:/to: ; *term* substring (pg_trgm). Fields and phrases are positive-only.
        internal static List<List<SearchClause>> ParseSearchClauses(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return new List<List<SearchClause>>();

            var validFields = new HashSet<string> { "subject", "body", "from", "to" };

            // Query = AND of OR-runs. Each item (word / phrase / field:value / field:(group)) yields a
            // CNF fragment (List<List<SearchClause>>). Items combine left-to-right by implicit AND; OR
            // merges neighbours. field:(...) groups parse recursively and re-map every clause onto that
            // field; OR between groups is resolved by CNF distribution, bounded by MaxClauseGroups.
            var regex = new Regex(
                @"(?<gneg>[-!]?)(?<gfield>\w+):\((?<ginner>[^)]*)\)" +
                @"|(?<pneg>[-!]?)""(?<phrase>[^""]*)""" +
                @"|(?<fneg>[-!]?)(?<field>\w+):(""(?<fq>[^""]*)""|(?<fu>\S+))" +
                @"|(?<tok>\S+)",
                RegexOptions.None);

            List<List<SearchClause>> result = null; // accumulated AND of completed OR-runs
            List<List<SearchClause>> run = null;     // current OR-run
            bool pendingOr = false;

            foreach (Match match in regex.Matches(searchTerm))
            {
                if (match.Groups["tok"].Success)
                {
                    var tv = match.Groups["tok"].Value.Trim();
                    if (tv.Equals("OR", StringComparison.OrdinalIgnoreCase) ||
                        tv.Equals("ODER", StringComparison.OrdinalIgnoreCase) || tv == "|")
                    {
                        if (run != null) pendingOr = true;
                        continue;
                    }
                }

                var frag = ParseItemToCnf(match, validFields);
                if (frag == null || frag.Count == 0)
                {
                    pendingOr = false; // a dropped item cannot serve as an OR operand
                    continue;
                }

                if (pendingOr && run != null)
                {
                    run = OrCnf(run, frag);
                    pendingOr = false;
                }
                else
                {
                    if (run != null)
                        result = result == null ? run : AndCnf(result, run);
                    run = frag;
                }
            }
            if (run != null)
                result = result == null ? run : AndCnf(result, run);

            return result ?? new List<List<SearchClause>>();
        }

        // Parses one regex match into a CNF fragment (null = nothing to add).
        private static List<List<SearchClause>> ParseItemToCnf(Match match, HashSet<string> validFields)
        {
            if (match.Groups["gfield"].Success)
            {
                var field = match.Groups["gfield"].Value.ToLower().Trim();
                var column = GetColumnForField(field);
                if (!validFields.Contains(field) || column == null)
                    return null;
                var inner = ParseSearchClauses(match.Groups["ginner"].Value);
                RemapGroupToField(inner, column);
                if (match.Groups["gneg"].Value.Length > 0)
                    inner = NegateCnf(inner);
                return inner.Count > 0 ? inner : null;
            }
            if (match.Groups["phrase"].Success)
            {
                var phrase = match.Groups["phrase"].Value.Trim();
                if (phrase.Length == 0) return null;
                return One(new SearchClause { Kind = ClauseKind.Phrase, Text = phrase, Negated = match.Groups["pneg"].Value.Length > 0 });
            }
            if (match.Groups["field"].Success)
            {
                var field = match.Groups["field"].Value.ToLower().Trim();
                var negated = match.Groups["fneg"].Value.Length > 0;
                var rawFieldVal = match.Groups["fq"].Success ? match.Groups["fq"].Value : match.Groups["fu"].Value;
                if (field == "has")
                {
                    if (IsAttachmentKeyword(rawFieldVal))
                        return One(new SearchClause { Kind = ClauseKind.Attachment, Negated = negated });
                    return null;
                }
                var column = GetColumnForField(field);
                if (validFields.Contains(field) && column != null)
                {
                    var term = Regex.Replace(rawFieldVal.Trim(), @"[&|!():\*]", "", RegexOptions.None);
                    if (term.Length > 0)
                        return One(new SearchClause { Kind = ClauseKind.Field, Text = term, Column = column, Negated = negated });
                }
                return null;
            }
            if (match.Groups["tok"].Success)
            {
                var token = match.Groups["tok"].Value.Trim();
                if (token.Length == 0) return null;
                bool negated = false;
                if ((token.StartsWith("-") || token.StartsWith("!")) && token.Length > 1)
                {
                    negated = true;
                    token = token.Substring(1);
                }
                if (token.Length > 2 && token.StartsWith("*") && token.EndsWith("*"))
                {
                    var inner = token.Substring(1, token.Length - 2); // LIKE metacharacters escaped at build time
                    if (inner.Length > 0)
                        return One(new SearchClause { Kind = ClauseKind.Substring, Text = inner, Negated = negated });
                    return null;
                }
                var sanitized = Regex.Replace(token, @"[&|!():\*<>'""]", "", RegexOptions.None);
                if (sanitized.Length > 0)
                    return One(new SearchClause { Kind = ClauseKind.Word, Text = sanitized, Negated = negated });
                return null;
            }
            return null;
        }

        private static List<List<SearchClause>> One(SearchClause c)
            => new List<List<SearchClause>> { new List<SearchClause> { c } };

        // Re-map every clause of a parsed group onto one field (POSITION-substring semantics).
        private static void RemapGroupToField(List<List<SearchClause>> cnf, string column)
        {
            for (int g = 0; g < cnf.Count; g++)
                for (int i = 0; i < cnf[g].Count; i++)
                {
                    var c = cnf[g][i];
                    cnf[g][i] = c.Kind == ClauseKind.Attachment
                        ? c
                        : new SearchClause { Kind = ClauseKind.Field, Text = c.Text, Column = column, Negated = c.Negated };
                }
        }

        // AND of two CNFs = concatenation of their groups (bounded).
        private static List<List<SearchClause>> AndCnf(List<List<SearchClause>> a, List<List<SearchClause>> b)
        {
            var r = new List<List<SearchClause>>(a);
            r.AddRange(b);
            if (r.Count > MaxClauseGroups) r = r.GetRange(0, MaxClauseGroups);
            return r;
        }

        // OR of two CNFs = distribute: each pair of groups merges its clause lists (bounded).
        private static List<List<SearchClause>> OrCnf(List<List<SearchClause>> a, List<List<SearchClause>> b)
        {
            var r = new List<List<SearchClause>>();
            foreach (var ga in a)
                foreach (var gb in b)
                {
                    var merged = new List<SearchClause>(ga);
                    merged.AddRange(gb);
                    r.Add(merged);
                    if (r.Count >= MaxClauseGroups) return r;
                }
            return r;
        }

        // De Morgan: NOT(AND_i OR_j c) = OR_i (AND_j !c), re-distributed back to CNF.
        private static List<List<SearchClause>> NegateCnf(List<List<SearchClause>> cnf)
        {
            List<List<SearchClause>> acc = null;
            foreach (var group in cnf)
            {
                var negGroup = new List<List<SearchClause>>();
                foreach (var c in group)
                    negGroup.Add(new List<SearchClause> { new SearchClause { Kind = c.Kind, Text = c.Text, Column = c.Column, Negated = !c.Negated } });
                acc = acc == null ? negGroup : OrCnf(acc, negGroup);
            }
            return acc ?? new List<List<SearchClause>>();
        }

        // Builds a GIN-indexable phrase tsquery ("w1 <-> w2 ...", prefix-matched) so exact-phrase
        // searches can use the full-text index as a prefilter before the POSITION recheck.
        private static string BuildPhraseTsQuery(string phrase)
        {
            if (string.IsNullOrWhiteSpace(phrase))
                return null;
            var terms = new List<string>();
            foreach (var word in phrase.Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
            {
                var sanitized = Regex.Replace(word, @"[&|!():\*]", "", RegexOptions.None);
                if (!string.IsNullOrEmpty(sanitized))
                    terms.Add(sanitized.Replace("'", "''") + ":*");
            }
            return terms.Count == 0 ? null : string.Join(" <-> ", terms);
        }

        private static string GetColumnForField(string field) => field switch
        {
            "subject" => "Subject",
            "body" => "Body",
            "from" => "From",
            "to" => "To",
            _ => null
        };

        internal static string WordAtom(SearchClause c)
        {
            var t = c.Text.Replace("'", "''");
            return (c.Negated ? "!" : "") + t + ":*";
        }

        // Efficient single combined tsquery for a pure-word query (AND of groups, OR within a group).
        internal static string BuildWordTsQuery(List<List<SearchClause>> groups)
            => string.Join(" & ", groups.Select(g =>
                g.Count == 1 ? WordAtom(g[0]) : "(" + string.Join(" | ", g.Select(WordAtom)) + ")"));

        // De Morgan dual of a pure-negation word query: NOT(a & (b|c)) == a | (b & c). Turns an
        // "exclude only" search into an index-usable positive set for an anti-membership filter.
        internal static string BuildNegationComplementTsQuery(List<List<SearchClause>> groups)
            => string.Join(" | ", groups.Select(g =>
                g.Count == 1 ? PosAtom(g[0]) : "(" + string.Join(" & ", g.Select(PosAtom)) + ")"));

        private static string PosAtom(SearchClause c)
        {
            var t = c.Text.Replace("'", "''");
            return t + ":*";
        }

        private (string OrderByClause, string SortColumn, bool IsTimestampSort) GetOrderByClause(string sortBy, string sortOrder)
        {
            var (columnName, isTimestampSort) = (sortBy?.ToLower()) switch
            {
                "subject" => ("Subject", false),
                "from" => ("From", false),
                "to" => ("To", false),
                "sentdate" => ("SentDate", true),
                "receiveddate" => ("ReceivedDate", true),
                _ => ("SentDate", true)
            };

            var direction = sortOrder?.ToLower() == "asc" ? "ASC" : "DESC";
            return ($@"ORDER BY e.""{columnName}"" {direction}", columnName, isTimestampSort);
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
                if (allowedAccountIds != null && !allowedAccountIds.Contains(accountId.Value))
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
                var groups = ParseSearchClauses(searchTerm);
                foreach (var group in groups)
                {
                    // OR within the group (any clause type), AND across groups; negated -> NOT.
                    System.Linq.Expressions.Expression<Func<ArchivedEmail, bool>> groupPredicate = null;
                    foreach (var clause in group)
                    {
                        var clausePredicate = ClausePredicate(clause);
                        groupPredicate = groupPredicate == null ? clausePredicate : OrElsePredicate(groupPredicate, clausePredicate);
                    }
                    if (groupPredicate != null)
                        searchQuery = searchQuery.Where(groupPredicate);
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
                        .ThenInclude(a => a.AttachmentContent)
                    .FirstOrDefaultAsync(e => e.Id == parameters.EmailId.Value);

                if (email == null)
                {
                    throw new InvalidOperationException($"Email with ID {parameters.EmailId.Value} not found");
                }

                await ExportSingleEmailAsEml(email, ms);
            }

            ms.Position = 0;
            return ms.ToArray();
        }

        private async Task ExportSingleEmailAsEml(ArchivedEmail email, MemoryStream ms)
        {
            var message = new MimeMessage();
            message.Subject = email.Subject;

            try { message.From.Add(InternetAddress.Parse(email.From)); }
            catch { message.From.Add(new MailboxAddress("Sender", "sender@example.com")); }

            // Apply stored display name to the From address if available
            MailContentHelper.ApplyDisplayNames(message.From, email.FromDisplayName);

            foreach (var to in email.To.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                try { message.To.Add(InternetAddress.Parse(to.Trim())); }
                catch { continue; }
            }

            MailContentHelper.ApplyDisplayNames(message.To, email.ToDisplayNames);

            if (!string.IsNullOrEmpty(email.Cc))
            {
                foreach (var cc in email.Cc.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    try { message.Cc.Add(InternetAddress.Parse(cc.Trim())); }
                    catch { continue; }
                }

                MailContentHelper.ApplyDisplayNames(message.Cc, email.CcDisplayNames);
            }

            if (!string.IsNullOrEmpty(email.Bcc))
            {
                foreach (var bcc in email.Bcc.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    try { message.Bcc.Add(InternetAddress.Parse(bcc.Trim())); }
                    catch { continue; }
                }

                MailContentHelper.ApplyDisplayNames(message.Bcc, email.BccDisplayNames);
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

            // Build the body so it faithfully reflects the original structure:
            // - genuine text + html  -> multipart/alternative (text/plain + text/html)
            // - only html (or text is HTML stored as fallback) -> text/html
            // - only text -> text/plain
            var hasHtmlToExport = !string.IsNullOrEmpty(htmlBodyToExport);
            var hasGenuineTextToExport = !string.IsNullOrEmpty(textBodyToExport)
                && !MailContentHelper.IsHtmlContent(textBodyToExport, htmlBodyToExport);

            MimeEntity body;
            if (hasHtmlToExport && hasGenuineTextToExport)
            {
                var alternative = new Multipart("alternative");
                alternative.Add(new TextPart("plain") { Text = textBodyToExport });
                alternative.Add(new TextPart("html") { Text = htmlBodyToExport });
                body = alternative;
            }
            else if (hasHtmlToExport)
            {
                body = new TextPart("html") { Text = htmlBodyToExport };
            }
            else
            {
                body = new TextPart("plain") { Text = textBodyToExport ?? string.Empty };
            }

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

            message.Date = _dateTimeHelper.ToDisplayTimeZoneOffset(email.SentDate);
            message.MessageId = email.MessageId;

            await Task.Run(() => message.WriteTo(ms));
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
                    AccountId = a.Id,
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
                    rawHeaders = MailContentHelper.CleanText(rawHeaders);
                }

                // Check if this email is already archived
            var messageId = message.MessageId ??
                $"{message.From}-{message.To}-{message.Subject}-{emailDate.Ticks}";

            var existingEmail = await _context.ArchivedEmails
                .FirstOrDefaultAsync(e => e.MessageId == messageId && e.MailAccountId == account.Id);

            if (existingEmail != null)
            {
                // E-Mail existiert bereits, prüfen ob der Ordner geändert wurde
                var cleanFolderName = MailContentHelper.CleanText(folderName ?? string.Empty);
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
                var subject = MailContentHelper.CleanText(message.Subject ?? "(No Subject)");
                // Extract email address from From field
                var fromAddress = message.From?.FirstOrDefault() as MailboxAddress;
                var from = MailContentHelper.CleanText(fromAddress?.Address ?? string.Empty);
                // Extract email addresses from To field
                var toAddresses = message.To?.Select(m => m as MailboxAddress).Where(m => m != null).Select(m => m.Address) ?? new List<string>();
                var to = MailContentHelper.CleanText(string.Join(", ", toAddresses));
                // Extract email addresses from Cc field
                var ccAddresses = message.Cc?.Select(m => m as MailboxAddress).Where(m => m != null).Select(m => m.Address) ?? new List<string>();
                var cc = MailContentHelper.CleanText(string.Join(", ", ccAddresses));
                // Extract email addresses from Bcc field
                var bccAddresses = message.Bcc?.Select(m => m as MailboxAddress).Where(m => m != null).Select(m => m.Address) ?? new List<string>();
                var bcc = MailContentHelper.CleanText(string.Join(", ", bccAddresses));

                // Extract display names (MailboxAddress.Name) for faithful restore/export
                var fromDisplayName = MailContentHelper.CleanText(fromAddress?.Name);
                var toDisplayNames = MailContentHelper.CleanText(string.Join(", ",
                    message.To?.Select(m => (m as MailboxAddress)?.Name)
                            .Where(n => !string.IsNullOrEmpty(n)) ?? Enumerable.Empty<string>()));
                var ccDisplayNames = MailContentHelper.CleanText(string.Join(", ",
                    message.Cc?.Select(m => (m as MailboxAddress)?.Name)
                            .Where(n => !string.IsNullOrEmpty(n)) ?? Enumerable.Empty<string>()));
                var bccDisplayNames = MailContentHelper.CleanText(string.Join(", ",
                    message.Bcc?.Select(m => (m as MailboxAddress)?.Name)
                            .Where(n => !string.IsNullOrEmpty(n)) ?? Enumerable.Empty<string>()));

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
                var originalTextBody = !string.IsNullOrEmpty(rawTextBody) ? MailContentHelper.CleanText(rawTextBody) : null;
                var originalHtmlBody = !string.IsNullOrEmpty(rawHtmlBody) ? MailContentHelper.CleanText(rawHtmlBody) : null;

                // Handle text body - use original content directly to preserve encoding
                if (!string.IsNullOrEmpty(message.TextBody))
                {
                    var cleanedTextBody = MailContentHelper.CleanText(message.TextBody);
                    // Check if text body needs truncation for tsvector compatibility
                    // Set to 500KB to ensure total of all fields stays under 1MB tsvector limit
                    if (Encoding.UTF8.GetByteCount(cleanedTextBody) > 500_000)
                    {
                        body = MailContentHelper.TruncateTextForStorage(cleanedTextBody, 500_000);
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
                    var cleanedHtmlAsText = MailContentHelper.CleanText(message.HtmlBody);
                    // Check if HTML-as-text body needs truncation for tsvector compatibility
                    // Set to 500KB to ensure total of all fields stays under 1MB tsvector limit
                    if (Encoding.UTF8.GetByteCount(cleanedHtmlAsText) > 500_000)
                    {
                        body = MailContentHelper.TruncateTextForStorage(cleanedHtmlAsText, 500_000);
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
                    htmlBody = MailContentHelper.CleanText(message.HtmlBody);

                    // Check if HTML body will be truncated
                    if (htmlBody.Length > 1_000_000)
                    {
                        htmlBody = MailContentHelper.CleanHtmlForStorage(htmlBody);
                    }
                }

                var cleanMessageId = MailContentHelper.CleanText(messageId);
                var cleanFolderName = MailContentHelper.CleanText(folderName ?? string.Empty);

                // Ensure individual fields don't exceed reasonable limits for tsvector
                // This prevents tsvector size errors when all fields are concatenated
                subject = MailContentHelper.TruncateFieldForTsvector(subject, 50_000); // ~50KB for subject
                from = MailContentHelper.TruncateFieldForTsvector(from, 10_000); // ~10KB for from
                to = MailContentHelper.TruncateFieldForTsvector(to, 50_000); // ~50KB for to (can be many recipients)
                cc = MailContentHelper.TruncateFieldForTsvector(cc, 50_000); // ~50KB for cc
                bcc = MailContentHelper.TruncateFieldForTsvector(bcc, 50_000); // ~50KB for bcc
                                                             // Body already truncated above

                fromDisplayName = MailContentHelper.TruncateFieldForTsvector(fromDisplayName, 50_000);
                toDisplayNames = MailContentHelper.TruncateFieldForTsvector(toDisplayNames, 50_000);
                ccDisplayNames = MailContentHelper.TruncateFieldForTsvector(ccDisplayNames, 50_000);
                bccDisplayNames = MailContentHelper.TruncateFieldForTsvector(bccDisplayNames, 50_000);

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
                        body = MailContentHelper.TruncateTextForStorage(body, maxBodySize);
                    }
                    else if (maxBodySize <= 0)
                    {
                        // Other fields alone exceed limit, truncate body completely
                        _logger.LogError("Other email fields alone exceed tsvector limit, body will be saved as attachment only");
                        body = "[Body too large - saved as attachment]";
                    }
                }

                body = MailContentHelper.SanitizeLongTokens(body);

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
                    FromDisplayName = string.IsNullOrEmpty(fromDisplayName) ? null : fromDisplayName,
                    ToDisplayNames = string.IsNullOrEmpty(toDisplayNames) ? null : toDisplayNames,
                    CcDisplayNames = string.IsNullOrEmpty(ccDisplayNames) ? null : ccDisplayNames,
                    BccDisplayNames = string.IsNullOrEmpty(bccDisplayNames) ? null : bccDisplayNames,
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

                            var cleanFileName = MailContentHelper.CleanText(fileName);
                            var contentType = MailContentHelper.CleanText(attachment.ContentType?.MimeType ?? "application/octet-stream");
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

                // Rescue floating text/calendar parts (Outlook/M365 meeting invitations).
                // These parts are neither regular attachments nor inline content and would
                // otherwise be silently dropped, leaving the archived email body empty.
                var calendarExtraction = CalendarContentHelper.TryExtractCalendar(message);
                if (calendarExtraction != null)
                {
                    if (string.IsNullOrEmpty(body))
                    {
                        body = CalendarContentHelper.ParseICalSummary(calendarExtraction.Content);
                        originalTextBody = body;
                        archivedEmail.Body = body;
                        archivedEmail.OriginalBodyText = Encoding.UTF8.GetBytes(body);
                    }

                    var icsBytes = Encoding.UTF8.GetBytes(calendarExtraction.Content);
                    var icsAttachment = new EmailAttachment
                    {
                        FileName = MailContentHelper.CleanText(calendarExtraction.FileName),
                        ContentType = MailContentHelper.CleanText(calendarExtraction.MimeType),
                        ContentId = null,
                        Content = icsBytes,
                        Size = icsBytes.Length
                    };
                    emailAttachments.Add(icsAttachment);
                    archivedEmail.Attachments.Add(icsAttachment);
                    archivedEmail.HasAttachments = true;
                }

                try
                {
                    _context.ArchivedEmails.Add(archivedEmail);
                    await _context.SaveChangesAsync();

                    // Attachments are already saved via EF relationship (cascade)
                    _logger.LogInformation("Successfully saved email with {Count} attachments", emailAttachments.Count);

                    _logger.LogInformation(
                        "Archived email: {Subject}, From: {From}, To: {To}, Account: {AccountName}, Attachments: {AttachmentCount}",
                        archivedEmail.Subject, archivedEmail.From, archivedEmail.To, account.Name, allAttachments.Count);

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

                // Unlock emails that fall under the retention policy so the database
                // compliance trigger (prevent_locked_email_deletion) allows the deletion.
                // Retention is exempt from the global DeletionPolicy lock.
                try
                {
                    await _context.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE mail_archiver.\"ArchivedEmails\" SET \"IsLocked\" = false WHERE \"MailAccountId\" = {account.Id} AND \"SentDate\" < {cutoffDate}");
                    _logger.LogDebug("Unlocked {Count} retention-expired emails for account {AccountName} prior to deletion",
                        emailsToDelete.Count, account.Name);
                }
                catch (Exception unlockEx)
                {
                    _logger.LogError(unlockEx, "Failed to unlock retention-expired emails for account {AccountName}", account.Name);
                }

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

        #region Folder Tree

        /// <summary>
        /// Gets the folder tree hierarchy with email counts for a specific account or all accounts
        /// </summary>
        /// <param name="accountId">Optional account ID to filter folders</param>
        /// <param name="allowedAccountIds">List of account IDs the user has access to</param>
        /// <returns>A list of folder tree nodes representing the folder hierarchy</returns>
        public async Task<List<FolderTreeNode>> GetFolderTreeAsync(int? accountId, List<int> allowedAccountIds = null)
        {
            try
            {
                // Build base query for folders
                var query = _context.ArchivedEmails.AsNoTracking();

                // Filter by account if specified
                if (accountId.HasValue)
                {
                    // Check access permission
                    if (allowedAccountIds != null && !allowedAccountIds.Contains(accountId.Value))
                    {
                        _logger.LogWarning("User attempted to access folder tree for account {AccountId} which is not in their allowed accounts list", accountId.Value);
                        return new List<FolderTreeNode>();
                    }
                    query = query.Where(e => e.MailAccountId == accountId.Value);
                }
                else if (allowedAccountIds != null)
                {
                    if (allowedAccountIds.Any())
                    {
                        query = query.Where(e => allowedAccountIds.Contains(e.MailAccountId));
                    }
                    else
                    {
                        // User has no allowed accounts
                        return new List<FolderTreeNode>();
                    }
                }

                // Get folder names with counts
                var folderData = await query
                    .Where(e => !string.IsNullOrEmpty(e.FolderName))
                    .GroupBy(e => e.FolderName)
                    .Select(g => new { FolderName = g.Key, Count = g.Count() })
                    .ToListAsync();

                if (!folderData.Any())
                {
                    return new List<FolderTreeNode>();
                }

                // Security: Validate folder names to prevent potential injection attacks
                const int maxFolderNameLength = 500;
                const int maxTotalFolders = 10000;
                
                var validFolderData = folderData
                    .Where(f => 
                        f.FolderName != null && 
                        f.FolderName.Length <= maxFolderNameLength &&
                        !f.FolderName.Contains("..") && // Prevent path traversal
                        !f.FolderName.Contains("<") &&  // Prevent XSS
                        !f.FolderName.Contains(">") &&
                        !f.FolderName.Contains("javascript:", StringComparison.OrdinalIgnoreCase))
                    .Take(maxTotalFolders)
                    .ToList();

                if (validFolderData.Count < folderData.Count)
                {
                    _logger.LogWarning("Folder tree validation filtered out {InvalidCount} suspicious folder names for account {AccountId}",
                        folderData.Count - validFolderData.Count, accountId);
                }

                return BuildFolderTree(validFolderData.Select(f => (f.FolderName, f.Count)).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting folder tree for account {AccountId}", accountId);
                return new List<FolderTreeNode>();
            }
        }

        // Builds the folder tree from (folderName, count) pairs. Extracted from GetFolderTreeAsync so
        // it can be unit-tested. Folder names that differ only in case are treated as one folder
        // (IMAP INBOX is case-insensitive): their counts are merged and the node is emitted once.
        internal static List<FolderTreeNode> BuildFolderTree(List<(string Name, int Count)> folders)
        {
            // Only create hierarchy when a parent folder actually exists in the data. This prevents
            // folder names containing '/' (or '.') from being split into phantom sub-hierarchies.
            var folderNameSet = new HashSet<string>(
                folders.Select(f => f.Name),
                StringComparer.OrdinalIgnoreCase);

            // Create nodes. If two folders differ only in case (e.g. "INBOX" and "Inbox" from two
            // accounts) merge their counts into a single node, so the same node is not emitted twice.
            var allNodes = new Dictionary<string, FolderTreeNode>(StringComparer.OrdinalIgnoreCase);
            foreach (var folder in folders)
            {
                if (allNodes.TryGetValue(folder.Name, out var existing))
                {
                    existing.TotalCount += folder.Count;
                    continue;
                }
                allNodes[folder.Name] = new FolderTreeNode
                {
                    Name = folder.Name,
                    FullPath = folder.Name,
                    TotalCount = folder.Count,
                    UnreadCount = 0,
                    Children = new List<FolderTreeNode>()
                };
            }

            // Build parent-child relationships. Iterate the DISTINCT nodes (not the raw folder list),
            // so each node is placed exactly once; process shortest paths first so parents exist first.
            var rootNodes = new List<FolderTreeNode>();
            foreach (var node in allNodes.Values.OrderBy(n => n.FullPath.Length).ThenBy(n => n.FullPath, StringComparer.Ordinal))
            {
                string? parentPath = null;

                // Find the nearest existing parent by scanning for hierarchy separators from right to left.
                // Common IMAP separators: '/' (most servers), '.', '\\' (rare)
                for (int i = node.FullPath.Length - 1; i >= 0; i--)
                {
                    if (node.FullPath[i] == '/' || node.FullPath[i] == '\\' || node.FullPath[i] == '.')
                    {
                        var candidate = node.FullPath.Substring(0, i);
                        if (folderNameSet.Contains(candidate))
                        {
                            parentPath = candidate;
                            break;
                        }
                    }
                }

                if (parentPath != null && allNodes.TryGetValue(parentPath, out var parentNode) && !ReferenceEquals(parentNode, node))
                {
                    // Child folder — display name is the suffix after the parent path
                    node.Name = node.FullPath.Substring(parentPath.Length + 1);
                    node.Level = parentNode.Level + 1;
                    parentNode.Children.Add(node);
                }
                else
                {
                    // Root folder — display name stays as the full folder name
                    node.Level = 0;
                    rootNodes.Add(node);
                }
            }

            return SortFolderTree(rootNodes);
        }

        /// <summary>
        /// Sorts the folder tree with INBOX at the top, then special folders, then alphabetically
        /// </summary>
        private static List<FolderTreeNode> SortFolderTree(List<FolderTreeNode> nodes)
        {
            if (nodes == null || !nodes.Any())
                return nodes;

            // Priority order for special folders
            var priorityFolders = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { "inbox", 1 },
                { "drafts", 2 },
                { "sent", 3 },
                { "junk", 4 },
                { "spam", 5 },
                { "trash", 6 },
                { "deleted", 7 },
                { "archive", 8 }
            };

            // Sort each level
            foreach (var node in nodes)
            {
                if (node.Children != null && node.Children.Any())
                {
                    node.Children = SortFolderTree(node.Children.ToList());
                }
            }

            // Sort this level
            return nodes
                .OrderBy(n =>
                {
                    // Check if this folder or any parent is a priority folder
                    var lowerName = n.Name.ToLowerInvariant();
                    if (priorityFolders.TryGetValue(lowerName, out var priority))
                        return priority;
                    
                    // Check if the full path contains a priority folder
                    var lowerPath = n.FullPath.ToLowerInvariant();
                    foreach (var pf in priorityFolders)
                    {
                        if (lowerPath.StartsWith(pf.Key + "/") || 
                            lowerPath.StartsWith(pf.Key + ".") ||
                            lowerPath == pf.Key)
                            return pf.Value;
                    }
                    
                    return 100; // Default priority for non-special folders
                })
                .ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        #endregion
    }
}

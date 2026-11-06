using Npgsql;
using System.Text.RegularExpressions;

namespace MailArchiver.Services.Core
{
    /// <summary>
    /// A SQL WHERE clause built from the archive search filters, together with the
    /// parameters it references and the next free parameter index.
    /// <see cref="MatchesNothing"/> is true when the filter can never match a row
    /// (for example an account the caller is not allowed to see); callers must
    /// return an empty result instead of running the query.
    /// </summary>
    internal sealed record EmailFilterClause(
        string WhereClause,
        List<NpgsqlParameter> Parameters,
        int NextParamIndex,
        bool MatchesNothing,
        string? NoMatchReason = null);

    /// <summary>
    /// Single source of truth for the archive filter semantics. Search, deletion
    /// preview and the filter-based deletion job all build their WHERE clause here,
    /// so a bulk delete can never use a subtly different definition of "matching"
    /// than the search the user confirmed.
    /// </summary>
    internal static class EmailFilterSql
    {
        internal static EmailFilterClause Build(
            string? searchTerm,
            DateTime? fromDate,
            DateTime? toDate,
            int? accountId,
            string? folderName,
            bool? isOutgoing,
            List<int>? allowedAccountIds,
            int startParamIndex = 0)
        {
            var whereConditions = new List<string>();
            var parameters = new List<Npgsql.NpgsqlParameter>();
            var paramCounter = startParamIndex;

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
                    var phraseTsQuery = BuildPhraseTsQuery(phrase);
                    if (!string.IsNullOrEmpty(phraseTsQuery))
                    {
                        searchConditions.Add($@"(
                        to_tsvector('simple', 
                            COALESCE(""Subject"", '') || ' ' || 
                            COALESCE(""Body"", '') || ' ' || 
                            COALESCE(""From"", '') || ' ' || 
                            COALESCE(""To"", '') || ' ' || 
                            COALESCE(""Cc"", '') || ' ' || 
                            COALESCE(""Bcc"", '')) 
                        @@ to_tsquery('simple', @param{paramCounter})
                        AND (
                        POSITION(LOWER(@param{paramCounter + 1}) IN LOWER(COALESCE(""Subject"", ''))) > 0 OR
                        POSITION(LOWER(@param{paramCounter + 1}) IN LOWER(COALESCE(""Body"", ''))) > 0 OR
                        POSITION(LOWER(@param{paramCounter + 1}) IN LOWER(COALESCE(""From"", ''))) > 0 OR
                        POSITION(LOWER(@param{paramCounter + 1}) IN LOWER(COALESCE(""To"", ''))) > 0 OR
                        POSITION(LOWER(@param{paramCounter + 1}) IN LOWER(COALESCE(""Cc"", ''))) > 0 OR
                        POSITION(LOWER(@param{paramCounter + 1}) IN LOWER(COALESCE(""Bcc"", ''))) > 0
                    ))");
                        parameters.Add(new Npgsql.NpgsqlParameter($"@param{paramCounter}", phraseTsQuery));
                        paramCounter++;
                        parameters.Add(new Npgsql.NpgsqlParameter($"@param{paramCounter}", phrase));
                        paramCounter++;
                    }
                    else
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
                if (allowedAccountIds != null && !allowedAccountIds.Contains(accountId.Value))
                {
                    return MatchesNothing(startParamIndex,
                        $"User attempted to access account {accountId.Value} which is not in their allowed accounts list");
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
                    return MatchesNothing(startParamIndex,
                        "User has no allowed accounts, returning empty result set");
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

            // Folder filtering: selecting a folder also shows the emails of all descendant
            // folders (path-boundary prefix match). Intermediate folders rendered in the
            // folder tree can hold no emails themselves; an exact-only match would return
            // an empty result for such parents. Kept in sync with SearchEmailsEFAsync.
            if (!string.IsNullOrEmpty(folderName))
            {
                var exactParam = paramCounter;
                var slashParam = paramCounter + 1;
                var backslashParam = paramCounter + 2;
                var dotParam = paramCounter + 3;
                paramCounter += 4;

                // Escape LIKE special chars (\ % _) in the literal part, then append the
                // wildcard LAST so it isn't escaped. The separator must be escaped too:
                // a backslash separator is itself the default LIKE escape character, so an
                // unescaped "…\%" would read as literal '%' instead of "prefix + anything".
                const string esc = "\\";
                string Esc(string s) => s.Replace(esc, esc + esc).Replace("%", esc + "%").Replace("_", esc + "_");

                whereConditions.Add($@"(""FolderName"" = @param{exactParam} OR ""FolderName"" LIKE @param{slashParam} ESCAPE '{esc}' OR ""FolderName"" LIKE @param{backslashParam} ESCAPE '{esc}' OR ""FolderName"" LIKE @param{dotParam} ESCAPE '{esc}')");
                parameters.Add(new Npgsql.NpgsqlParameter($"@param{exactParam}", folderName));
                parameters.Add(new Npgsql.NpgsqlParameter($"@param{slashParam}",     Esc(folderName + "/")  + "%"));
                parameters.Add(new Npgsql.NpgsqlParameter($"@param{backslashParam}", Esc(folderName + "\\") + "%"));
                parameters.Add(new Npgsql.NpgsqlParameter($"@param{dotParam}",       Esc(folderName + ".")  + "%"));
            }

            var whereClause = whereConditions.Any() ? "WHERE " + string.Join(" AND ", whereConditions) : "";
            return new EmailFilterClause(whereClause, parameters, paramCounter, MatchesNothing: false);
        }

        private static EmailFilterClause MatchesNothing(int nextParamIndex, string reason)
            => new(string.Empty, new List<NpgsqlParameter>(), nextParamIndex, MatchesNothing: true, reason);

        internal static (string tsQuery, List<string> phrases, Dictionary<string, List<string>> fieldSearches, Dictionary<string, List<string>> fieldPhrases) ParseSearchTermForTsQuery(string searchTerm)
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
                // Use prefix matching (:*) for each term to enable partial word matching
                // This allows "isenb" to match "isenboeck", "isenböck", etc.
                // The GIN index supports prefix matching efficiently
                var escapedTerms = individualWords.Select(t => t.Replace("'", "''") + ":*");
                tsQuery = string.Join(" & ", escapedTerms);
            }

            return (tsQuery, phrases, fieldSearches, fieldPhrases);
        }

        internal static string? GetColumnNameForField(string fieldName)
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

        /// <summary>
        /// Builds a GIN-indexable tsquery for an exact phrase or single term, using prefix
        /// matching (:*) and adjacency (<->) between words. Returns null when the input
        /// contains no usable tokens (only punctuation), in which case the caller should
        /// fall back to POSITION-only matching.
        /// Used as a selective pre-filter before the authoritative POSITION substring check,
        /// so the GIN index narrows the candidate set and the body is not detoasted during
        /// matching for the common case.
        /// </summary>
        internal static string? BuildPhraseTsQuery(string phrase)
        {
            if (string.IsNullOrWhiteSpace(phrase))
                return null;

            var words = phrase.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            var escapedTerms = new List<string>(words.Length);
            foreach (var word in words)
            {
                var sanitized = Regex.Replace(word, @"[&|!():\*]", "", RegexOptions.None);
                if (!string.IsNullOrEmpty(sanitized))
                    escapedTerms.Add(sanitized.Replace("'", "''") + ":*");
            }

            if (escapedTerms.Count == 0)
                return null;

            return string.Join(" <-> ", escapedTerms);
        }
    }
}

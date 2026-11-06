using MailArchiver.Data;
using MailArchiver.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using System.Data;
using System.Data.Common;

namespace MailArchiver.Services.Core
{
    /// <summary>
    /// Counts and enumerates the emails matching a deletion filter. Both operations
    /// use <see cref="EmailFilterSql"/>, the same builder as the archive search, and
    /// run on the caller's connection and transaction: the deletion job must see the
    /// rows of its own transaction, and the test fixture relies on the ambient
    /// transaction to roll everything back.
    /// </summary>
    internal static class EmailFilterQuery
    {
        internal static async Task<(int Deletable, int Locked, int? MaxId)> CountAsync(
            MailArchiverDbContext context,
            EmailDeletionFilter filter,
            CancellationToken cancellationToken)
        {
            var clause = BuildClause(filter);
            if (clause.MatchesNothing)
                return (0, 0, null);

            var bound = string.Empty;
            if (filter.UpperBoundId.HasValue)
            {
                bound = $@" AND ""Id"" <= @param{clause.NextParamIndex}";
                clause.Parameters.Add(new NpgsqlParameter($"@param{clause.NextParamIndex}", filter.UpperBoundId.Value));
            }

            var sql = $@"
                SELECT COUNT(*) FILTER (WHERE NOT ""IsLocked"")::int AS deletable,
                       COUNT(*) FILTER (WHERE ""IsLocked"")::int     AS locked,
                       MAX(""Id"") FILTER (WHERE NOT ""IsLocked"")     AS max_id
                FROM mail_archiver.""ArchivedEmails""
                {clause.WhereClause}{bound}";

            using var command = await CreateCommandAsync(context, sql, clause.Parameters, cancellationToken);
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return (0, 0, null);

            var deletable = reader.GetInt32(0);
            var locked = reader.GetInt32(1);
            var maxId = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2);
            return (deletable, locked, maxId);
        }

        internal static async Task<List<int>> IdsBatchAsync(
            MailArchiverDbContext context,
            EmailDeletionFilter filter,
            int afterId,
            int take,
            CancellationToken cancellationToken)
        {
            var clause = BuildClause(filter);
            if (clause.MatchesNothing || take <= 0)
                return new List<int>();

            var conditions = new List<string> { @"""IsLocked"" = false" };
            var parameters = clause.Parameters;
            var paramIndex = clause.NextParamIndex;

            conditions.Add($@"""Id"" > @param{paramIndex}");
            parameters.Add(new NpgsqlParameter($"@param{paramIndex}", afterId));
            paramIndex++;

            if (filter.UpperBoundId.HasValue)
            {
                conditions.Add($@"""Id"" <= @param{paramIndex}");
                parameters.Add(new NpgsqlParameter($"@param{paramIndex}", filter.UpperBoundId.Value));
                paramIndex++;
            }

            var wherePrefix = string.IsNullOrEmpty(clause.WhereClause)
                ? "WHERE "
                : clause.WhereClause + " AND ";

            var sql = $@"
                SELECT ""Id""
                FROM mail_archiver.""ArchivedEmails""
                {wherePrefix}{string.Join(" AND ", conditions)}
                ORDER BY ""Id""
                LIMIT @param{paramIndex}";

            parameters.Add(new NpgsqlParameter($"@param{paramIndex}", take));

            var ids = new List<int>();
            using var command = await CreateCommandAsync(context, sql, parameters, cancellationToken);
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                ids.Add(reader.GetInt32(0));

            return ids;
        }

        private static EmailFilterClause BuildClause(EmailDeletionFilter filter)
            => EmailFilterSql.Build(
                filter.SearchTerm,
                filter.FromDate,
                filter.ToDate,
                filter.AccountId,
                filter.FolderName,
                filter.IsOutgoing,
                filter.AllowedAccountIds);

        private static async Task<DbCommand> CreateCommandAsync(
            MailArchiverDbContext context,
            string sql,
            IEnumerable<NpgsqlParameter> parameters,
            CancellationToken cancellationToken)
        {
            // OpenConnectionAsync keeps EF aware of the open connection, so the
            // context closes it again when it is disposed.
            if (context.Database.GetDbConnection().State != ConnectionState.Open)
                await context.Database.OpenConnectionAsync(cancellationToken);

            var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = 120;

            if (context.Database.CurrentTransaction != null)
                command.Transaction = context.Database.CurrentTransaction.GetDbTransaction();

            foreach (var parameter in parameters)
                command.Parameters.Add(parameter);

            return command;
        }
    }
}

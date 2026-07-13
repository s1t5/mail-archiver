using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MailArchiver.Services.Core;
using Npgsql;
using Xunit;

namespace MailArchiver.Tests;

// DB-backed robustness test: every tsquery the parser produces for word clauses must be a
// syntactically valid PostgreSQL tsquery (else the optimized search throws and falls back).
// Runs only when MAILARCHIVER_TEST_DB is set (CI / opt-in).
public class TsQueryValidityTests
{
    private static string? Conn => Environment.GetEnvironmentVariable("MAILARCHIVER_TEST_DB");

    public static IEnumerable<object[]> Inputs => new[]
    {
        "rechnung", "rechnung mahnung", "auto OR fahrrad", "rechnung -mahnung",
        "wd red 8 tb", "auto rad OR bike", "-mahnung", "\"exact phrase\"",
        "subject:invoice", "from:a OR from:b", "*x* OR *y*", "invoice OR from:acme",
        "a&b|c", "(foo)", "***", "- - -", "OR OR OR", "x27); --", "a:*b",
        "buero strasse", "cafe", "8TB WD80EFPX", "a;b:c,d.e@f", "!!!", "()()",
    }.Select(s => new object[] { s });

    [Theory]
    [MemberData(nameof(Inputs))]
    public async Task Parser_tsqueries_are_accepted_by_postgres(string input)
    {
        if (Conn == null) return; // integration test: only runs when MAILARCHIVER_TEST_DB is set (CI)
        var groups = EmailCoreService.ParseSearchClauses(input);
        var queries = new List<string>();
        if (groups.Count > 0 && groups.All(g => g.All(c => c.Kind == EmailCoreService.ClauseKind.Word)))
            queries.Add(EmailCoreService.BuildWordTsQuery(groups));
        else
            queries.AddRange(groups.SelectMany(g => g)
                .Where(c => c.Kind == EmailCoreService.ClauseKind.Word)
                .Select(EmailCoreService.WordAtom));

        await using var c = new NpgsqlConnection(Conn);
        await c.OpenAsync();
        foreach (var q in queries.Where(q => !string.IsNullOrEmpty(q)))
        {
            await using var cmd = new NpgsqlCommand("SELECT to_tsquery('simple', @q)", c);
            cmd.Parameters.AddWithValue("q", q);
            var ex = await Record.ExceptionAsync(() => cmd.ExecuteScalarAsync());
            Assert.True(ex == null, $"Parser emitted an invalid tsquery for [{input}] -> [{q}]: {ex?.Message}");
        }
    }
}

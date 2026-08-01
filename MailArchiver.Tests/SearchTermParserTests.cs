using System.Collections.Generic;
using System.Linq;
using MailArchiver.Services.Core;
using Xunit;

namespace MailArchiver.Tests;

// Unit tests for the unified search-clause parser (words / phrases / fields / substrings,
// with OR-groups and negation). Word tsquery composition is checked via BuildWordTsQuery.
public class SearchTermParserTests
{
    private static List<List<EmailCoreService.SearchClause>> Parse(string? s)
        => EmailCoreService.ParseSearchClauses(s!);

    // combined tsquery for pure-word queries
    private static string Ts(string? s)
        => EmailCoreService.BuildWordTsQuery(EmailCoreService.ParseSearchClauses(s!));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_input_yields_no_groups(string? input)
        => Assert.Empty(Parse(input));

    // ---- word tsquery (AND / OR / NOT / conditional prefix) ----
    [Fact] public void Single_long_word_gets_prefix() => Assert.Equal("rechnung:*", Ts("rechnung"));

    [Theory]
    [InlineData("wd")]
    [InlineData("tb")]
    public void Short_word_gets_prefix(string input) => Assert.Equal(input + ":*", Ts(input));

    [Fact] public void Multi_word_is_AND() => Assert.Equal("rechnung:* & mahnung:*", Ts("rechnung mahnung"));
    [Fact] public void Mixed_length_words() => Assert.Equal("wd:* & red:* & 8:* & tb:*", Ts("wd red 8 tb"));

    [Theory]
    [InlineData("auto OR fahrrad")]
    [InlineData("auto or fahrrad")]
    [InlineData("auto ODER fahrrad")]
    [InlineData("auto | fahrrad")]
    public void Or_creates_group(string input) => Assert.Equal("(auto:* | fahrrad:*)", Ts(input));

    [Fact] public void Or_binds_neighbours() => Assert.Equal("invoice:* & (car:* | bike:*)", Ts("invoice car OR bike"));
    [Fact] public void And_then_or() => Assert.Equal("auto:* & (rad:* | bike:*)", Ts("auto rad OR bike"));
    [Fact] public void Chained_or() => Assert.Equal("(aaa:* | bbb:* | ccc:*)", Ts("aaa OR bbb OR ccc"));

    [Theory]
    [InlineData("-mahnung")]
    [InlineData("!mahnung")]
    public void Exclude_negates(string input) => Assert.Equal("!mahnung:*", Ts(input));

    [Fact] public void And_with_exclude() => Assert.Equal("rechnung:* & !mahnung:*", Ts("rechnung -mahnung"));

    // ---- typed clauses ----
    [Fact]
    public void Phrase_clause()
    {
        var c = Assert.Single(Assert.Single(Parse("\"exact phrase\"")));
        Assert.Equal(EmailCoreService.ClauseKind.Phrase, c.Kind);
        Assert.Equal("exact phrase", c.Text);
    }

    [Fact]
    public void Negated_phrase_clause()
    {
        var c = Assert.Single(Assert.Single(Parse("-\"exact phrase\"")));
        Assert.Equal(EmailCoreService.ClauseKind.Phrase, c.Kind);
        Assert.Equal("exact phrase", c.Text);
        Assert.True(c.Negated);
    }

    [Fact]
    public void Bang_negated_phrase_clause()
        => Assert.True(Assert.Single(Assert.Single(Parse("!\"exact phrase\""))).Negated);

    [Fact]
    public void Phrase_with_leading_dash_inside_quotes_is_not_negated()
    {
        var c = Assert.Single(Assert.Single(Parse("\"-foo\"")));
        Assert.Equal("-foo", c.Text);
        Assert.False(c.Negated);
    }

    [Fact]
    public void Included_and_excluded_phrase_are_two_groups()
    {
        var groups = Parse("\"offene rechnung\" -\"bereits bezahlt\"");
        Assert.Equal(2, groups.Count);
        var inc = Assert.Single(groups[0]);
        Assert.Equal(EmailCoreService.ClauseKind.Phrase, inc.Kind);
        Assert.False(inc.Negated);
        var exc = Assert.Single(groups[1]);
        Assert.Equal(EmailCoreService.ClauseKind.Phrase, exc.Kind);
        Assert.True(exc.Negated);
    }

    [Fact]
    public void Has_attachment_clause()
    {
        var c = Assert.Single(Assert.Single(Parse("has:attachment")));
        Assert.Equal(EmailCoreService.ClauseKind.Attachment, c.Kind);
        Assert.False(c.Negated);
    }

    [Fact]
    public void Has_attachment_negated()
    {
        var c = Assert.Single(Assert.Single(Parse("-has:attachment")));
        Assert.Equal(EmailCoreService.ClauseKind.Attachment, c.Kind);
        Assert.True(c.Negated);
    }

    [Fact]
    public void Has_attachment_german_keyword()
        => Assert.Equal(EmailCoreService.ClauseKind.Attachment, Assert.Single(Assert.Single(Parse("has:anhang"))).Kind);

    [Fact]
    public void Has_unknown_keyword_ignored() => Assert.Empty(Parse("has:banana"));

    [Fact]
    public void Text_with_attachment_filter_two_groups()
    {
        var groups = Parse("rechnung has:attachment");
        Assert.Equal(2, groups.Count);
        Assert.Contains(groups.SelectMany(g => g), c => c.Kind == EmailCoreService.ClauseKind.Attachment);
        Assert.Contains(groups.SelectMany(g => g), c => c.Kind == EmailCoreService.ClauseKind.Word && c.Text == "rechnung");
    }

    // ---- field:(...) groups (Gmail-style), full boolean logic ----
    [Fact]
    public void Field_group_terms_are_anded()
    {
        var groups = Parse("from:(meier schulze)");
        Assert.Equal(2, groups.Count);
        Assert.All(groups, g => Assert.Equal("From", Assert.Single(g).Column));
        Assert.All(groups, g => Assert.Equal(EmailCoreService.ClauseKind.Field, Assert.Single(g).Kind));
        Assert.Contains(groups.SelectMany(x => x), c => c.Text == "meier");
        Assert.Contains(groups.SelectMany(x => x), c => c.Text == "schulze");
    }

    [Fact]
    public void Field_group_phrase_is_one_field_clause()
    {
        var c = Assert.Single(Assert.Single(Parse("from:(\"meier schulze\")")));
        Assert.Equal(EmailCoreService.ClauseKind.Field, c.Kind);
        Assert.Equal("From", c.Column);
        Assert.Equal("meier schulze", c.Text);
    }

    [Fact]
    public void Field_group_or_is_one_group()
    {
        var g = Assert.Single(Parse("from:(meier OR schulze)"));
        Assert.Equal(2, g.Count);
        Assert.All(g, c => Assert.Equal("From", c.Column));
    }

    [Fact]
    public void Field_group_with_exclude()
    {
        var flat = Parse("subject:(rechnung -storno)").SelectMany(x => x);
        Assert.Contains(flat, c => c.Column == "Subject" && c.Text == "rechnung" && !c.Negated);
        Assert.Contains(flat, c => c.Column == "Subject" && c.Text == "storno" && c.Negated);
    }

    [Fact]
    public void Or_between_two_single_term_field_groups()
    {
        var g = Assert.Single(Parse("from:(meier) OR to:(schulze)"));
        Assert.Equal(2, g.Count);
        Assert.Contains(g, c => c.Column == "From" && c.Text == "meier");
        Assert.Contains(g, c => c.Column == "To" && c.Text == "schulze");
    }

    [Fact]
    public void Or_between_group_and_term_distributes()
    {
        // (from:a AND from:b) OR to:c  ==  (from:a OR to:c) AND (from:b OR to:c)
        var groups = Parse("from:(a b) OR to:(c)");
        Assert.Equal(2, groups.Count);
        Assert.All(groups, g => Assert.Contains(g, c => c.Column == "To" && c.Text == "c"));
        Assert.Contains(groups, g => g.Any(c => c.Column == "From" && c.Text == "a"));
        Assert.Contains(groups, g => g.Any(c => c.Column == "From" && c.Text == "b"));
    }

    [Fact]
    public void Negated_field_group_demorgan()
    {
        // -from:(a b) == NOT(from:a AND from:b) == (NOT from:a OR NOT from:b): one group, both negated
        var g = Assert.Single(Parse("-from:(a b)"));
        Assert.Equal(2, g.Count);
        Assert.All(g, c => Assert.True(c.Negated && c.Column == "From"));
    }

    [Fact]
    public void Invalid_field_group_ignored() => Assert.Empty(Parse("bogus:(x y)"));

    [Fact]
    public void Field_group_mixes_with_explicit_prefix()
    {
        var flat = Parse("from:(rechnung) subject:eilig").SelectMany(x => x);
        Assert.Contains(flat, c => c.Column == "From" && c.Text == "rechnung");
        Assert.Contains(flat, c => c.Column == "Subject" && c.Text == "eilig");
    }

    [Fact]
    public void Field_clause()
    {
        var c = Assert.Single(Assert.Single(Parse("subject:invoice")));
        Assert.Equal(EmailCoreService.ClauseKind.Field, c.Kind);
        Assert.Equal("Subject", c.Column);
        Assert.Equal("invoice", c.Text);
    }

    [Fact]
    public void Substring_clause()
    {
        var c = Assert.Single(Assert.Single(Parse("*teil*")));
        Assert.Equal(EmailCoreService.ClauseKind.Substring, c.Kind);
        Assert.Equal("teil", c.Text);
        Assert.False(c.Negated);
    }

    [Fact]
    public void Negated_substring() => Assert.True(Assert.Single(Assert.Single(Parse("-*teil*"))).Negated);

    [Fact]
    public void Negation_complement_dual()
        => Assert.Equal("rechnung:* | wd:* | 8:*", EmailCoreService.BuildNegationComplementTsQuery(EmailCoreService.ParseSearchClauses("-rechnung -wd -8")));

    // Codex P2 (#17): the pure-negation complement must split punctuated literals the SAME way as
    // WordAtom. -O'Reilly must yield the positive complement (O & Reilly:*), never the invalid
    // O''Reilly:* that Postgres' tsquery parser rejects (which would fall back to the slow EF scan).
    [Fact]
    public void Negation_complement_splits_apostrophe_literal()
        => Assert.Equal("(O & Reilly:*)", EmailCoreService.BuildNegationComplementTsQuery(EmailCoreService.ParseSearchClauses("-O'Reilly")));

    [Fact]
    public void Negation_complement_splits_ampersand_literal()
        => Assert.Equal("(R & D:*)", EmailCoreService.BuildNegationComplementTsQuery(EmailCoreService.ParseSearchClauses("-R&D")));

    [Fact]
    public void Substring_keeps_like_metacharacters()
        => Assert.Equal("INV_2026", Assert.Single(Assert.Single(Parse("*INV_2026*"))).Text);

    [Fact]
    public void Unknown_field_is_ignored() => Assert.Empty(Parse("bogus:value"));

    // ---- OR across non-word types (Codex regression tests) ----
    [Fact]
    public void Or_across_fields_is_one_group()
    {
        var groups = Parse("from:alice OR from:bob");
        var g = Assert.Single(groups);                       // one OR-group, not two AND-groups
        Assert.Equal(2, g.Count);
        Assert.All(g, c => Assert.Equal(EmailCoreService.ClauseKind.Field, c.Kind));
        Assert.Equal(new[] { "alice", "bob" }, g.Select(c => c.Text));
    }

    [Fact]
    public void Or_across_substrings_is_one_group()
    {
        var g = Assert.Single(Parse("*invoice* OR *receipt*"));
        Assert.Equal(2, g.Count);
        Assert.All(g, c => Assert.Equal(EmailCoreService.ClauseKind.Substring, c.Kind));
    }

    [Fact]
    public void Mixed_word_or_field_is_one_group()
    {
        var g = Assert.Single(Parse("invoice OR from:acme"));
        Assert.Equal(2, g.Count);
        Assert.Contains(g, c => c.Kind == EmailCoreService.ClauseKind.Word && c.Text == "invoice");
        Assert.Contains(g, c => c.Kind == EmailCoreService.ClauseKind.Field && c.Text == "acme");
    }

    [Fact]
    public void Complex_combination_groups()
    {
        // phrase, word, !word, field, substring -> 5 AND-groups (no OR)
        var groups = Parse("\"car insurance\" rechnung -spam subject:invoice *teil*");
        Assert.Equal(5, groups.Count);
        Assert.All(groups, g => Assert.Single(g));
        Assert.Contains(groups.SelectMany(g => g), c => c.Kind == EmailCoreService.ClauseKind.Phrase);
        Assert.Contains(groups.SelectMany(g => g), c => c.Kind == EmailCoreService.ClauseKind.Field && c.Text == "invoice");
        Assert.Contains(groups.SelectMany(g => g), c => c.Kind == EmailCoreService.ClauseKind.Substring && c.Text == "teil");
        Assert.Contains(groups.SelectMany(g => g), c => c.Kind == EmailCoreService.ClauseKind.Word && c.Negated && c.Text == "spam");
    }

    [Theory]
    [InlineData("a&b|c")]
    [InlineData("(foo)")]
    [InlineData("x27); --")]
    [InlineData("- - -")]
    [InlineData("OR OR OR")]
    public void Adversarial_never_throws(string input)
        => Assert.Null(Record.Exception(() => Parse(input)));

    // ---- literal preservation (Codex P2 fixes) ----
    [Fact]
    public void Field_quoted_value_keeps_syntax_chars()
    {
        var c = Assert.Single(Assert.Single(Parse("subject:\"R&D\"")));
        Assert.Equal(EmailCoreService.ClauseKind.Field, c.Kind);
        Assert.Equal("Subject", c.Column);
        Assert.Equal("R&D", c.Text);
    }

    [Fact]
    public void Field_quoted_value_keeps_apostrophe_and_parens()
    {
        var c = Assert.Single(Assert.Single(Parse("subject:\"Meeting (Q&A)\"")));
        Assert.Equal("Meeting (Q&A)", c.Text);
    }

    [Fact]
    public void Word_keeps_apostrophe_literal()
    {
        var c = Assert.Single(Assert.Single(Parse("O'Reilly")));
        Assert.Equal(EmailCoreService.ClauseKind.Word, c.Kind);
        Assert.Equal("O'Reilly", c.Text);
    }

    // O'Reilly is split on the apostrophe (as the simple parser lexes the document: o, reilly);
    // exact-match all parts but the last (prefix), parenthesised so OR/negation compose.
    [Fact]
    public void Word_apostrophe_splits_into_lexemes() => Assert.Equal("(O & Reilly:*)", Ts("O'Reilly"));

    [Fact]
    public void Word_ampersand_splits_into_lexemes() => Assert.Equal("(R & D:*)", Ts("R&D"));

    // ---- Codex P2 (#3): OR-distribution bound is sound AND non-silent ----
    // A normal boolean query stays well under MaxClauseGroups -> not truncated.
    [Fact]
    public void Normal_query_is_not_truncated()
    {
        EmailCoreService.ParseSearchClauses("auto OR fahrrad OR bike", out var truncated);
        Assert.False(truncated);
    }

    // A pathological OR distribution ( >256 CNF groups ) is bounded and reports truncated=true,
    // so the caller can log instead of silently returning partial (over-approximated) results.
    [Fact]
    public void Over_bound_or_distribution_sets_truncated_and_bounds_groups()
    {
        var many = string.Join(" ", Enumerable.Range(1, 300).Select(i => "z" + i));
        var groups = EmailCoreService.ParseSearchClauses($"subject:(festplatte) OR body:({many})", out var truncated);
        Assert.True(truncated);
        Assert.True(groups.Count <= 256, $"expected <=256 bounded groups, got {groups.Count}");
    }

    // Codex P2 (#17): a NEGATED field group whose inner conjunction is truncated cannot be soundly
    // negated -- NOT(w1..w256) wrongly excludes mails satisfying the full NOT(w1..w300). It must be
    // dropped (sound superset -> recall preserved) and report truncated=true, never emit a negated
    // partial group.
    [Fact]
    public void Negated_truncated_field_group_is_dropped_not_negated()
    {
        var many = string.Join(" ", Enumerable.Range(1, 300).Select(i => "z" + i));
        var groups = EmailCoreService.ParseSearchClauses($"-subject:({many})", out var truncated);
        Assert.True(truncated);
        Assert.Empty(groups);
    }

    // Control: a small negated field group is still negated normally (drop only on truncation).
    [Fact]
    public void Small_negated_field_group_is_negated_normally()
    {
        var groups = EmailCoreService.ParseSearchClauses("-subject:(spam werbung)", out var truncated);
        Assert.False(truncated);
        Assert.NotEmpty(groups);
    }

    // Regression: a quoted value inside a field group may contain ')' — the group scanner
    // must not terminate ginner at the parenthesis inside the quotes (P2, Codex 2a1c0db).
    [Fact]
    public void Field_group_preserves_quoted_parentheses()
    {
        var c = Assert.Single(Assert.Single(Parse("subject:(\"Meeting (Q&A)\")")));
        Assert.Equal("Meeting (Q&A)", c.Text);
        Assert.Equal(EmailCoreService.ClauseKind.Field, c.Kind);
    }

    // ReDoS regression: a malformed field group with many quotes and no closing ')' must parse in
    // linear time (non-overlapping regex branches), not explore exponentially many partitions (P1).
    [Fact]
    public void Malformed_field_group_with_many_quotes_does_not_backtrack()
    {
        var evil = "subject:(" + new string('"', 60);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var _ = Parse(evil);
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 1000, $"parse took {sw.ElapsedMilliseconds}ms");
    }
}


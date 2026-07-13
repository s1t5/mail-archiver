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
}

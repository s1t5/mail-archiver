using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace MailArchiver.Tests;

// End-to-end search tests against a seeded, disposable PostgreSQL database.
// Runs the REAL optimized SQL path (SearchEmailsOptimizedAsync) and asserts the
// exact set of matching e-mail ids. Requires MAILARCHIVER_TEST_DB_RW pointing at a
// DISPOSABLE database (the fixture creates and drops the mail_archiver schema).
public class SearchDbFixture : IAsyncLifetime
{
    public string? Conn => Environment.GetEnvironmentVariable("MAILARCHIVER_TEST_DB_RW");
    public bool Enabled => !string.IsNullOrEmpty(Conn);

    private const string Ddl = @"
DROP SCHEMA IF EXISTS mail_archiver CASCADE;
CREATE SCHEMA mail_archiver;
CREATE TABLE mail_archiver.""MailAccounts""(""Id"" int PRIMARY KEY, ""Name"" text, ""EmailAddress"" text);
CREATE TABLE mail_archiver.""ArchivedEmails""(
  ""Id"" int PRIMARY KEY, ""MailAccountId"" int, ""MessageId"" text,
  ""Subject"" text, ""Body"" text, ""HtmlBody"" text,
  ""From"" text, ""To"" text, ""Cc"" text, ""Bcc"" text,
  ""SentDate"" timestamp, ""ReceivedDate"" timestamp,
  ""IsOutgoing"" boolean, ""HasAttachments"" boolean, ""FolderName"" text, ""IsLocked"" boolean);
INSERT INTO mail_archiver.""MailAccounts"" VALUES (1,'Privat','privat@x.de'),(2,'Arbeit','arbeit@x.de');
INSERT INTO mail_archiver.""ArchivedEmails"" VALUES
 (1,1,'m1','WD Red Pro 8 TB Festplatte','Rechnung fuer die Festplatte','','shop@cyberport.de','me@x.de','','','2025-01-06'::timestamp,'2025-01-06'::timestamp,false,false,'INBOX',false),
 (2,2,'m2','Newsletter Angebote','auto und fahrrad im angebot','','news@shop.de','me@x.de','','','2025-03-01','2025-03-01',false,false,'INBOX',false),
 (3,1,'m3','Zahlungserinnerung','offene rechnung mahnung bitte zahlen','','billing@x.de','me@x.de','','','2025-02-01','2025-02-01',false,false,'INBOX',false),
 (4,2,'m4','Quartalsabrechnung','die abrechnung liegt bei','','buchhaltung@x.de','me@x.de','','','2025-04-01','2025-04-01',false,false,'INBOX',false),
 (5,1,'m5','Fahrrad Tour','wir fahren mit dem fahrrad','','club@x.de','me@x.de','','','2025-05-01','2025-05-01',false,false,'INBOX',false),
 (6,1,'m6','R&D O''Reilly Q&A','budget planning notes','','team@x.de','me@x.de','','','2025-06-01','2025-06-01',false,false,'INBOX',false);
CREATE TABLE mail_archiver.""EmailAttachments""(
  ""Id"" int PRIMARY KEY, ""ArchivedEmailId"" int, ""FileName"" text,
  ""ContentType"" text, ""ContentId"" text, ""Size"" bigint);
-- Mail 1 & 4 have a REAL file attachment (ContentId NULL); note HasAttachments=false on mail 1,
-- proving has:attachment no longer depends on the flag. Mail 5 has ONLY an inline signature image
-- (cid ContentId) -> must NOT count as 'has attachment'.
INSERT INTO mail_archiver.""EmailAttachments"" VALUES
 (1,1,'rechnung.pdf','application/pdf',NULL,1234),
 (2,4,'abrechnung.pdf','application/pdf',NULL,2345),
 (3,5,'inline_logo','image/png','logo@signatur',345);";

    private async Task Exec(string sql)
    {
        await using var c = new NpgsqlConnection(Conn);
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, c);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task InitializeAsync() { if (Enabled) await Exec(Ddl); }
    public async Task DisposeAsync() { if (Enabled) await Exec("DROP SCHEMA IF EXISTS mail_archiver CASCADE;"); }
}

public class SearchEndToEndTests : IClassFixture<SearchDbFixture>
{
    private readonly SearchDbFixture _fx;
    public SearchEndToEndTests(SearchDbFixture fx) => _fx = fx;

    private EmailCoreService Svc()
    {
        var opts = new DbContextOptionsBuilder<MailArchiverDbContext>().UseNpgsql(_fx.Conn).Options;
        var ctx = new MailArchiverDbContext(opts);
        return new EmailCoreService(ctx, NullLogger<EmailCoreService>.Instance, null!, Options.Create(new BatchOperationOptions()), new ConfigurationBuilder().Build());
    }

    private async Task<HashSet<int>> Ids(string term)
    {
        var (emails, total) = await Svc().SearchEmailsOptimizedAsync(term, null, null, null, null, null, 0, 100);
        Assert.Equal(total, emails.Count);
        return emails.Select(e => e.Id).ToHashSet();
    }

    [Fact] public async Task And_returns_only_docs_with_all_words()
    { if (!_fx.Enabled) return; Assert.Equal(new HashSet<int>{3}, await Ids("rechnung mahnung")); }

    [Fact] public async Task Single_word_prefix_not_midword()
    { if (!_fx.Enabled) return; Assert.Equal(new HashSet<int>{1,3}, await Ids("rechnung")); }

    [Fact] public async Task Exclude_removes_matches()
    { if (!_fx.Enabled) return; Assert.Equal(new HashSet<int>{1}, await Ids("rechnung -mahnung")); }

    [Fact] public async Task Or_returns_union()
    { if (!_fx.Enabled) return; Assert.Equal(new HashSet<int>{2,5}, await Ids("auto OR fahrrad")); }

    [Fact] public async Task Substring_matches_midword()
    { if (!_fx.Enabled) return; Assert.Equal(new HashSet<int>{1,3,4}, await Ids("*rechn*")); }

    [Fact] public async Task Field_subject()
    { if (!_fx.Enabled) return; Assert.Equal(new HashSet<int>{1}, await Ids("subject:festplatte")); }

    [Fact] public async Task Field_from()
    { if (!_fx.Enabled) return; Assert.Equal(new HashSet<int>{1}, await Ids("from:cyberport")); }

    [Fact] public async Task Short_token_finds()
    { if (!_fx.Enabled) return; Assert.Equal(new HashSet<int>{1}, await Ids("wd")); }

    [Fact] public async Task Or_across_fields() // Codex: from:a OR from:b
    { if (!_fx.Enabled) return; Assert.Equal(new HashSet<int>{1,3}, await Ids("from:cyberport OR from:billing")); }

    [Fact] public async Task Or_across_substrings() // Codex: *a* OR *b*
    { if (!_fx.Enabled) return; Assert.Equal(new HashSet<int>{1,2,5}, await Ids("*cyber* OR *fahrr*")); }

    [Fact] public async Task Phrase_matches_exact()
    { if (!_fx.Enabled) return; Assert.Equal(new HashSet<int>{1}, await Ids("\"WD Red\"")); }

    // has:attachment must match only REAL file attachments (EmailAttachments with ContentId IS NULL),
    // independent of the ArchivedEmails.HasAttachments flag.
    [Fact] public async Task Has_attachment_matches_only_real_files()
    { if (!_fx.Enabled) return; Assert.Equal(new HashSet<int>{1,4}, await Ids("has:attachment")); }

    // A mail whose only attachment is an inline signature image (cid ContentId) must NOT match.
    [Fact] public async Task Has_attachment_ignores_inline_images()
    { if (!_fx.Enabled) return; Assert.DoesNotContain(5, await Ids("has:attachment")); }

    [Fact] public async Task Negated_has_attachment_returns_complement()
    { if (!_fx.Enabled) return; Assert.Equal(new HashSet<int>{2,3,5,6}, await Ids("-has:attachment")); }

    [Fact] public async Task Word_and_has_attachment_intersect()
    { if (!_fx.Enabled) return; Assert.Equal(new HashSet<int>{1}, await Ids("rechnung has:attachment")); }

    // Codex P2 fix: quoted field values keep syntax chars (subject:"R&D" must match "R&D").
    [Fact] public async Task Field_quoted_value_with_ampersand()
    { if (!_fx.Enabled) return; Assert.Equal(new HashSet<int>{6}, await Ids("subject:\"R&D\"")); }

    // Codex P2 fix: quoted field value with an apostrophe matches literally (POSITION path).
    [Fact] public async Task Field_quoted_value_with_apostrophe()
    { if (!_fx.Enabled) return; Assert.Equal(new HashSet<int>{6}, await Ids("subject:\"O'Reilly\"")); }

    // Codex P2 fix: a plain word with an apostrophe still matches via FTS (O'Reilly -> lexeme oreilly).
    [Fact] public async Task Word_with_apostrophe_matches()
    { if (!_fx.Enabled) return; Assert.Equal(new HashSet<int>{6}, await Ids("O'Reilly")); }
}

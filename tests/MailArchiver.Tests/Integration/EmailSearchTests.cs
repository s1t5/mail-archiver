using System.Net;
using System.Text.Json;

namespace MailArchiver.Tests.Integration;

[Collection("Integration")]
public class EmailSearchTests : ApiTestBase
{
    public EmailSearchTests(PostgresContainerFixture fixture) : base(fixture) { }

    // Account A (visible to the limited user) holds 14 seeded emails.
    private const int AccountATotal = 14;

    private async Task<JsonElement> SearchAsync(string key, string query)
    {
        var response = await Client(key).GetAsync("/api/v1/emails" + query);
        response.EnsureSuccessStatusCode();
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    private static int TotalItems(JsonElement r) => r.GetProperty("totalItems").GetInt32();
    private static JsonElement Items(JsonElement r) => r.GetProperty("items");

    [Fact]
    public async Task NoFilter_LimitedUser_ReturnsOnlyOwnAccountEmails()
    {
        var r = await SearchAsync(Data.LimitedKey, "");
        Assert.Equal(AccountATotal, TotalItems(r));
        Assert.Equal(1, r.GetProperty("totalPages").GetInt32());
    }

    [Fact]
    public async Task NoFilter_Admin_SeesAllAccounts()
    {
        var r = await SearchAsync(Data.AdminKey, "?pageSize=100");
        // 14 (account A) + 2 (account B) = 16.
        Assert.True(TotalItems(r) >= 16, $"expected >= 16, got {TotalItems(r)}");
    }

    [Fact]
    public async Task FolderFilter_RestrictsResults()
    {
        var r = await SearchAsync(Data.LimitedKey, "?folder=INBOX/Work");
        Assert.Equal(2, TotalItems(r));
        foreach (var item in Items(r).EnumerateArray())
        {
            Assert.Equal("INBOX/Work", item.GetProperty("folderName").GetString());
        }
    }

    [Fact]
    public async Task DirectionFilter_Outgoing_ReturnsSentEmails()
    {
        var r = await SearchAsync(Data.LimitedKey, "?direction=outgoing");
        Assert.Equal(2, TotalItems(r));
        foreach (var item in Items(r).EnumerateArray())
        {
            Assert.True(item.GetProperty("isOutgoing").GetBoolean());
        }
    }

    [Fact]
    public async Task DateRangeFilter_LimitsToWindow()
    {
        // March 2026 account-A emails: 6 invoices + 3 body-chain + 1 attachment = 10.
        var r = await SearchAsync(Data.LimitedKey, "?from=2026-03-01&to=2026-03-31");
        Assert.Equal(10, TotalItems(r));
    }

    [Fact]
    public async Task AccountFilter_ForeignAccount_LimitedUser_ReturnsNothing()
    {
        var r = await SearchAsync(Data.LimitedKey, $"?accountId={Data.AccountBId}");
        Assert.Equal(0, TotalItems(r));
    }

    [Fact]
    public async Task AccountFilter_ZeroGrantUser_CannotBypassWithAccountId()
    {
        // Regression: an active non-admin with no UserMailAccounts must not be able
        // to read another account's emails by supplying ?accountId= explicitly.
        // Previously the .Any() guard in EmailCoreService let an empty allowed list
        // fall through to the account filter instead of denying.
        var rA = await SearchAsync(Data.NoGrantsKey, $"?accountId={Data.AccountAId}");
        Assert.Equal(0, TotalItems(rA));
        Assert.Equal(0, Items(rA).GetArrayLength());

        var rB = await SearchAsync(Data.NoGrantsKey, $"?accountId={Data.AccountBId}");
        Assert.Equal(0, TotalItems(rB));
    }

    [Fact]
    public async Task NoFilter_ZeroGrantUser_ReturnsNothing()
    {
        var r = await SearchAsync(Data.NoGrantsKey, "?pageSize=100");
        Assert.Equal(0, TotalItems(r));
    }

    [Fact]
    public async Task FullTextQuery_MatchesSubject()
    {
        var r = await SearchAsync(Data.LimitedKey, "?q=Invoice");
        Assert.Equal(6, TotalItems(r));
        foreach (var item in Items(r).EnumerateArray())
        {
            Assert.Contains("Invoice", item.GetProperty("subject").GetString());
        }
    }

    [Fact]
    public async Task Pagination_SplitsResults()
    {
        var page1 = await SearchAsync(Data.LimitedKey, "?pageSize=5&page=1");
        Assert.Equal(AccountATotal, TotalItems(page1));
        Assert.Equal(5, Items(page1).GetArrayLength());
        Assert.Equal(3, page1.GetProperty("totalPages").GetInt32());

        var page3 = await SearchAsync(Data.LimitedKey, "?pageSize=5&page=3");
        Assert.Equal(4, Items(page3).GetArrayLength()); // 14 - 2*5
    }

    [Fact]
    public async Task PageSize_IsClampedToMax()
    {
        var r = await SearchAsync(Data.LimitedKey, "?pageSize=1000");
        Assert.Equal(100, r.GetProperty("pageSize").GetInt32()); // MaxPageSize
    }

    [Fact]
    public async Task Sort_BySubjectAscending()
    {
        var r = await SearchAsync(Data.LimitedKey, "?sortBy=subject&sortOrder=asc&pageSize=100");
        var first = Items(r).EnumerateArray().First().GetProperty("subject").GetString();
        Assert.StartsWith("Body chain", first);
    }

    [Fact]
    public async Task InvalidSortBy_Returns400()
    {
        var response = await Client(Data.LimitedKey).GetAsync("/api/v1/emails?sortBy=bogus");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task InvalidDirection_Returns400()
    {
        var response = await Client(Data.LimitedKey).GetAsync("/api/v1/emails?direction=sideways");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

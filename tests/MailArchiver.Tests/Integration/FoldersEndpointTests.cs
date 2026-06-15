using System.Net;
using System.Text.Json;

namespace MailArchiver.Tests.Integration;

[Collection("Integration")]
public class FoldersEndpointTests : ApiTestBase
{
    public FoldersEndpointTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task LimitedUser_GetsFolderTreeForPermittedAccount()
    {
        var response = await Client(Data.LimitedKey).GetAsync($"/api/v1/accounts/{Data.AccountAId}/folders");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();

        // Seeded folders for account A include INBOX and the nested INBOX/Work.
        Assert.Contains("INBOX", body);
        Assert.Contains("Work", body);
    }

    [Fact]
    public async Task FolderTree_IsNestedWithExpectedFields()
    {
        var response = await Client(Data.AdminKey).GetAsync($"/api/v1/accounts/{Data.AccountAId}/folders");
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        var first = doc.RootElement.EnumerateArray().First();
        Assert.True(first.TryGetProperty("fullPath", out _));
        Assert.True(first.TryGetProperty("totalCount", out _));
        Assert.True(first.TryGetProperty("children", out _));
    }

    [Fact]
    public async Task LimitedUser_CannotSeeForeignAccountFolders_Returns404()
    {
        var response = await Client(Data.LimitedKey).GetAsync($"/api/v1/accounts/{Data.AccountBId}/folders");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Admin_CanSeeAnyAccountFolders()
    {
        var response = await Client(Data.AdminKey).GetAsync($"/api/v1/accounts/{Data.AccountBId}/folders");
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task NonexistentAccount_Returns404()
    {
        var response = await Client(Data.AdminKey).GetAsync("/api/v1/accounts/999999/folders");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

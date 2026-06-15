using System.Net;
using System.Text.Json;

namespace MailArchiver.Tests.Integration;

[Collection("Integration")]
public class AccountsEndpointTests : ApiTestBase
{
    public AccountsEndpointTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Admin_SeesAllAccounts()
    {
        var response = await Client(Data.AdminKey).GetAsync("/api/v1/accounts");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains(SeededData.AccountAName, body);
        Assert.Contains(SeededData.AccountBName, body);
    }

    [Fact]
    public async Task LimitedUser_SeesOnlyPermittedAccounts()
    {
        var response = await Client(Data.LimitedKey).GetAsync("/api/v1/accounts");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains(SeededData.AccountAName, body);
        Assert.DoesNotContain(SeededData.AccountBName, body);
    }

    [Fact]
    public async Task AccountPayload_NeverContainsCredentials()
    {
        var response = await Client(Data.AdminKey).GetAsync("/api/v1/accounts");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();

        // Neither the secret values nor the credential property names leak.
        Assert.DoesNotContain(SeededData.AccountASecretPassword, body);
        Assert.DoesNotContain(SeededData.AccountASecretClientSecret, body);
        Assert.DoesNotContain("\"password\"", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("clientSecret", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AccountDto_ExposesExpectedFields()
    {
        var response = await Client(Data.AdminKey).GetAsync("/api/v1/accounts");
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        var first = doc.RootElement.EnumerateArray().First();
        Assert.True(first.TryGetProperty("emailAddress", out _));
        Assert.True(first.TryGetProperty("provider", out _));
        Assert.True(first.TryGetProperty("isEnabled", out _));
        Assert.True(first.TryGetProperty("lastSync", out _));
    }
}

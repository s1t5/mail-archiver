using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MailArchiver.Tests.Integration;

[Collection("Integration")]
public class AuthenticationTests
{
    private readonly PostgresContainerFixture _fixture;

    public AuthenticationTests(PostgresContainerFixture fixture) => _fixture = fixture;

    private HttpClient NewClient(string? bearer = null)
    {
        var client = _fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        if (bearer != null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        }
        return client;
    }

    [Fact]
    public async Task NoApiKey_Returns401ProblemJson_NotLoginRedirect()
    {
        var client = NewClient();

        var response = await client.GetAsync("/api/v1/accounts");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Found, response.StatusCode); // never 302 -> /Auth/Login
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains(response.Headers.WwwAuthenticate, h => h.Scheme == "Bearer");
    }

    [Fact]
    public async Task GarbageKey_Returns401()
    {
        var client = NewClient("ma_not-a-real-key");

        var response = await client.GetAsync("/api/v1/accounts");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RevokedKey_Returns401()
    {
        var client = NewClient(_fixture.Data.RevokedKey);

        var response = await client.GetAsync("/api/v1/accounts");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ExpiredKey_Returns401()
    {
        var client = NewClient(_fixture.Data.ExpiredKey);

        var response = await client.GetAsync("/api/v1/accounts");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task KeyOfInactiveUser_Returns401()
    {
        var client = NewClient(_fixture.Data.InactiveUserKey);

        var response = await client.GetAsync("/api/v1/accounts");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ValidKey_PassesAuthentication()
    {
        var client = NewClient(_fixture.Data.LimitedKey);

        var response = await client.GetAsync("/api/v1/accounts");

        // Auth succeeded: not a 401 and not a login redirect. (The concrete
        // endpoint behaviour is covered by the endpoint test classes.)
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Found, response.StatusCode);
    }

    [Fact]
    public async Task ApiDisabled_Returns404()
    {
        using var disabledFactory = new ApiWebApplicationFactory(_fixture.ConnectionString, apiEnabled: false);
        var client = disabledFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _fixture.Data.AdminKey);

        var response = await client.GetAsync("/api/v1/accounts");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

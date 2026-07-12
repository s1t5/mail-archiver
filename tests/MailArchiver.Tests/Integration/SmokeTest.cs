using System.Net;

namespace MailArchiver.Tests.Integration;

/// <summary>
/// Proves the whole integration harness works end to end: the app boots under
/// <see cref="ApiWebApplicationFactory"/>, startup migrations apply against the
/// Testcontainers PostgreSQL instance, and the pipeline serves requests.
/// </summary>
[Collection("Integration")]
public class SmokeTest
{
    private readonly PostgresContainerFixture _fixture;

    public SmokeTest(PostgresContainerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task LoginPage_IsReachable_ProvingStartupAndMigrations()
    {
        var client = _fixture.Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/Auth/Login");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

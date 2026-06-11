using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace MailArchiver.Tests.Integration;

/// <summary>
/// Shared helpers for endpoint integration tests: a bearer-authenticated client
/// over the shared factory, and quick access to the seeded data. Derived classes
/// must carry their own <c>[Collection("Integration")]</c> attribute (xUnit does
/// not inherit it).
/// </summary>
public abstract class ApiTestBase
{
    protected readonly PostgresContainerFixture Fixture;

    protected ApiTestBase(PostgresContainerFixture fixture) => Fixture = fixture;

    protected SeededData Data => Fixture.Data;

    protected HttpClient Client(string? bearer = null)
    {
        var client = Fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        if (bearer != null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        }
        return client;
    }

    /// <summary>A DI scope over the running app (e.g. to read the AccessLogs table).</summary>
    protected IServiceScope NewScope() => Fixture.Factory.Services.CreateScope();
}

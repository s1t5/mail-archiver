using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;

namespace MailArchiver.Tests.Integration;

/// <summary>
/// Locks the generated OpenAPI document to the spec in doc/API.md: every promised
/// path must be present, so the spec and the controllers cannot drift apart. Also
/// verifies the Swagger UI sits behind the cookie middleware.
/// </summary>
[Collection("Integration")]
public class OpenApiContractTests : ApiTestBase
{
    public OpenApiContractTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Spec_ContainsAllPromisedV1Paths()
    {
        using var scope = NewScope();
        var provider = scope.ServiceProvider.GetRequiredKeyedService<IOpenApiDocumentProvider>("v1");
        var document = await provider.GetOpenApiDocumentAsync();

        var paths = document.Paths.Keys.ToList();
        Assert.Contains("/api/v1/accounts", paths);
        Assert.Contains("/api/v1/accounts/{id}/folders", paths);
        Assert.Contains("/api/v1/emails", paths);
        Assert.Contains("/api/v1/emails/{id}", paths);
        Assert.Contains("/api/v1/emails/{id}/attachments/{attachmentId}", paths);
    }

    [Fact]
    public async Task SwaggerUi_WithoutCookie_RedirectsToLogin()
    {
        var client = Fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/apidocs");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains("/Auth/Login", response.Headers.Location?.ToString() ?? string.Empty);
    }
}

using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace MailArchiver.Tests.Integration;

/// <summary>
/// Boots the real application against a Testcontainers PostgreSQL instance.
/// Overrides only what is required to run headless and deterministically:
/// the connection string, a dummy auth password (the app calls
/// Environment.Exit(1) when it is empty), an isolated DataProtection key path,
/// and <c>Api:Enabled=true</c>. All background <see cref="IHostedService"/>s
/// (mail sync, maintenance, dedup) are removed so tests are not perturbed by
/// nondeterministic background work.
/// </summary>
public sealed class ApiWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly string _dataProtectionPath;
    private readonly bool _apiEnabled;

    public ApiWebApplicationFactory(string connectionString, bool apiEnabled = true)
    {
        _connectionString = connectionString;
        _apiEnabled = apiEnabled;
        _dataProtectionPath = Path.Combine(Path.GetTempPath(), "ma-tests-dp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dataProtectionPath);

        // Program.cs reads several settings *eagerly* (during builder
        // configuration, before Build()): the DataProtection key path, the auth
        // password/enabled flag. ConfigureAppConfiguration delegates only apply
        // at Build() time and are therefore too late for those. Setting them as
        // environment variables makes them visible to WebApplication.CreateBuilder
        // itself. (Connection string is read lazily, so it is fine via config.)
        Environment.SetEnvironmentVariable("DataProtection__KeyPath", _dataProtectionPath);
        Environment.SetEnvironmentVariable("Authentication__Password", "test-password-123!");
        Environment.SetEnvironmentVariable("Authentication__Enabled", "true");
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", _connectionString);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString,
                ["Authentication:Username"] = "admin",
                ["Authentication:Password"] = "test-password-123!",
                ["DataProtection:KeyPath"] = _dataProtectionPath,
                ["Api:Enabled"] = _apiEnabled ? "true" : "false",
                ["Api:AllowAttachmentDownloads"] = "true",
                ["Api:EnableSwaggerUi"] = "true",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove background services: sync, maintenance, dedup, etc. They
            // would otherwise run against the test database nondeterministically.
            services.RemoveAll<IHostedService>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            try { Directory.Delete(_dataProtectionPath, recursive: true); }
            catch { /* best effort cleanup */ }
        }
    }
}

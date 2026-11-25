using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services.Monitoring;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace MailArchiver.Tests.Services;

/// <summary>
/// The readiness and freshness checks feed the monitoring endpoints, so their failure modes are
/// pinned here: a missing or unreachable database must be Unhealthy, stale accounts must be
/// Degraded (not Unhealthy, since the process still serves requests), and a disabled freshness
/// check must never touch the database.
/// </summary>
public class MonitoringHealthCheckTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly MailArchiverDbContext _context;

    public MonitoringHealthCheckTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<MailArchiverDbContext>()
            .UseSqlite(_connection)
            .Options;
        _context = new MailArchiverDbContext(options);
        _context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private static HealthCheckContext Context(IHealthCheck check) => new()
    {
        Registration = new HealthCheckRegistration(
            "test", check, HealthStatus.Unhealthy, Array.Empty<string>())
    };

    [Fact]
    public async Task DatabaseCheck_WithoutConnectionString_IsUnhealthy()
    {
        var configuration = new ConfigurationBuilder().Build();
        var check = new DatabaseHealthCheck(configuration);

        var result = await check.CheckHealthAsync(Context(check));

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("No database connection string", result.Description);
    }

    [Fact]
    public async Task DatabaseCheck_UnreachableServer_IsUnhealthy()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Port 1 on loopback refuses the connection immediately, so the check fails fast
                // without waiting for the two-second budget.
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=127.0.0.1;Port=1;Username=none;Password=none;Timeout=1;Command Timeout=1"
            })
            .Build();
        var check = new DatabaseHealthCheck(configuration);

        var result = await check.CheckHealthAsync(Context(check));

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("not reachable", result.Description);
    }

    [Fact]
    public async Task SyncCheck_Disabled_IsHealthyWithoutQuerying()
    {
        // No account rows exist; a check that queried anyway would still be Healthy, so the
        // description is what proves the early return.
        var check = new SyncFreshnessHealthCheck(
            new StubScopeFactory(_context),
            Options.Create(new HealthOptions { SyncStaleMinutes = 0 }));

        var result = await check.CheckHealthAsync(Context(check));

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("disabled", result.Description);
    }

    [Fact]
    public async Task SyncCheck_AllAccountsFresh_IsHealthy()
    {
        AddAccount("fresh", enabled: true, lastSync: DateTime.UtcNow.AddMinutes(-1));

        var check = new SyncFreshnessHealthCheck(
            new StubScopeFactory(_context),
            Options.Create(new HealthOptions { SyncStaleMinutes = 60 }));

        var result = await check.CheckHealthAsync(Context(check));

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task SyncCheck_StaleEnabledAccount_IsDegraded()
    {
        AddAccount("fresh", enabled: true, lastSync: DateTime.UtcNow.AddMinutes(-1));
        AddAccount("stale", enabled: true, lastSync: DateTime.UtcNow.AddHours(-3));
        // A disabled account that never synced must not count: it is not supposed to sync.
        AddAccount("disabled", enabled: false, lastSync: DateTime.UtcNow.AddDays(-30));

        var check = new SyncFreshnessHealthCheck(
            new StubScopeFactory(_context),
            Options.Create(new HealthOptions { SyncStaleMinutes = 60 }));

        var result = await check.CheckHealthAsync(Context(check));

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("1 enabled account", result.Description);
    }

    private void AddAccount(string name, bool enabled, DateTime lastSync)
    {
        _context.MailAccounts.Add(new MailAccount
        {
            Name = name,
            EmailAddress = $"{name}@example.org",
            LastSync = lastSync,
            IsEnabled = enabled
        });
        _context.SaveChanges();
    }

    /// <summary>
    /// Hands the test's DbContext to the check without a container: the check only asks for a
    /// scope and resolves <see cref="MailArchiverDbContext"/> from it.
    /// </summary>
    private sealed class StubScopeFactory : IServiceScopeFactory, IServiceScope, IServiceProvider
    {
        private readonly MailArchiverDbContext _context;

        public StubScopeFactory(MailArchiverDbContext context) => _context = context;

        public IServiceScope CreateScope() => this;

        public IServiceProvider ServiceProvider => this;

        public object? GetService(Type serviceType)
            => serviceType == typeof(MailArchiverDbContext) ? _context : null;

        public void Dispose()
        {
            // The test owns the context.
        }
    }
}

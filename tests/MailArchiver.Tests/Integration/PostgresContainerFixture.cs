using Testcontainers.PostgreSql;

namespace MailArchiver.Tests.Integration;

/// <summary>
/// Owns the shared integration-test state for the whole run: a single
/// PostgreSQL container (postgres:14-alpine, matching docker-compose.yml) and
/// the <see cref="ApiWebApplicationFactory"/> built on it. The factory boots
/// the real app, which runs the application's startup migrations against the
/// container — so a green run also proves the migrations apply cleanly.
///
/// Created once per run via the "Integration" collection so the container
/// starts (and is later seeded) exactly once.
/// </summary>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:14-alpine")
        .WithDatabase("mailarchiver_test")
        .WithUsername("mailtest")
        .WithPassword("mailtest")
        .Build();

    public ApiWebApplicationFactory Factory { get; private set; } = null!;

    public SeededData Data { get; private set; } = null!;

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        Factory = new ApiWebApplicationFactory(ConnectionString);

        // Force the host to build now so startup migrations run (and surface
        // any failure here rather than inside the first test).
        _ = Factory.Services;

        // Seed the shared data set exactly once for the whole run.
        Data = await TestDataSeeder.SeedAsync(Factory.Services);
    }

    public async Task DisposeAsync()
    {
        Factory?.Dispose();
        await _container.DisposeAsync();
    }
}

/// <summary>
/// Binds <see cref="PostgresContainerFixture"/> to the "Integration" collection
/// so every integration test class shares the one container and factory.
/// </summary>
[CollectionDefinition("Integration")]
public sealed class IntegrationCollection : ICollectionFixture<PostgresContainerFixture>
{
}

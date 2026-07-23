using MailArchiver.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace MailArchiver.Tests.Infrastructure;

/// <summary>
/// Shared fixture that builds a <see cref="MailArchiverDbContext"/> against the configured
/// test PostgreSQL database and applies pending EF Core migrations once per test session.
/// Connection string resolution order:
///   1. appsettings.Test.json      (tests/MailArchiver.Tests/appsettings.Test.json, gitignored)
///   2. appsettings.Development.json (repo root, gitignored — exists locally)
///   3. appsettings.json            (repo root, Docker defaults)
/// Each test gets its own <see cref="MailArchiverDbContext"/> instance; tests are expected to
/// clean up their own rows (or use <see cref="CreateTransactionalContextAsync"/> for rollback).
/// </summary>
public class TestDbFixture : IAsyncLifetime
{
    public const string CollectionName = "PostgreSQL collection";

    private IConfiguration _configuration = null!;
    private DbContextOptions<MailArchiverDbContext> _options = null!;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        // Mirror Program.cs: enable the legacy timestamp behavior so Unspecified DateTimes
        // can be written to timestamptz columns (matching the production app behavior).
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        _configuration = BuildConfiguration();
        ConnectionString = ResolveConnectionString();

        _options = new DbContextOptionsBuilder<MailArchiverDbContext>()
            .UseNpgsql(ConnectionString, npgsql => npgsql.CommandTimeout(120))
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        // Apply pending migrations once for the session so the schema is present.
        using var ctx = CreateContext();
        await ctx.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        // Nothing to dispose at the session level; contexts are created per-test.
        await Task.CompletedTask;
    }

    public MailArchiverDbContext CreateContext()
        => new(_options);

    /// <summary>
    /// Creates a context and begins a transaction. The returned <see cref="TestTxScope"/>
    /// owns both and disposes them on <see cref="TestTxScope.DisposeAsync"/>. Call
    /// <see cref="TestTxScope.RollbackAsync"/> at the end of the test to avoid persistence.
    /// </summary>
    public async Task<TestTxScope> CreateTransactionalContextAsync()
    {
        var ctx = CreateContext();
        var tx = await ctx.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        return new TestTxScope(ctx, tx);
    }

    /// <summary>
    /// Owns a <see cref="MailArchiverDbContext"/> plus its ambient transaction. Disposing
    /// rolls back the transaction (if not already committed/rolled back) and disposes the context.
    /// </summary>
    public sealed class TestTxScope : IAsyncDisposable
    {
        public MailArchiverDbContext Context { get; }
        private readonly IDbContextTransaction _tx;
        private bool _finalized;

        public TestTxScope(MailArchiverDbContext context, IDbContextTransaction tx)
        {
            Context = context;
            _tx = tx;
        }

        public Task RollbackAsync()
        {
            _finalized = true;
            return _tx.RollbackAsync();
        }

        public Task CommitAsync()
        {
            _finalized = true;
            return _tx.CommitAsync();
        }

        public async ValueTask DisposeAsync()
        {
            if (!_finalized)
            {
                try { await _tx.RollbackAsync(); } catch { }
            }
            await Context.DisposeAsync();
            _tx.Dispose();
        }
    }

    private IConfiguration BuildConfiguration()
    {
        var baseDir = AppContext.BaseDirectory;
        var repoRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", ".."));

        var builder = new ConfigurationBuilder()
            .SetBasePath(baseDir)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
            .AddJsonFile("appsettings.Test.json", optional: true, reloadOnChange: false)
            .AddJsonFile(Path.Combine(repoRoot, "appsettings.json"), optional: true, reloadOnChange: false)
            .AddJsonFile(Path.Combine(repoRoot, "appsettings.Development.json"), optional: true, reloadOnChange: false)
            .AddEnvironmentVariables(prefix: "MailArchiverTest__");

        return builder.Build();
    }

    private string ResolveConnectionString()
    {
        var cs = _configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(cs)) return cs;

        var host = _configuration["POSTGRES_HOST"] ?? "localhost";
        var db   = _configuration["POSTGRES_DB"]   ?? "MailArchiver";
        var user = _configuration["POSTGRES_USER"] ?? "mailuser";
        var pass = _configuration["POSTGRES_PASSWORD"] ?? "masterkey";
        return $"Host={host};Database={db};Username={user};Password={pass}";
    }
}

[CollectionDefinition(TestDbFixture.CollectionName)]
public class PostgresTestCollection : ICollectionFixture<TestDbFixture> { }
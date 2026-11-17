using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace MailArchiver.Services.Monitoring;

/// <summary>
/// Readiness check: PostgreSQL answers and the archive schema exists. The check uses its own
/// short-lived connection with a two-second budget, so a hanging database cannot hold the
/// health request open.
/// </summary>
public sealed class DatabaseHealthCheck : IHealthCheck
{
    private const int TimeoutSeconds = 2;

    private readonly string? _connectionString;

    public DatabaseHealthCheck(IConfiguration configuration)
        => _connectionString = configuration.GetConnectionString("DefaultConnection");

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return HealthCheckResult.Unhealthy("No database connection string is configured.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(timeout.Token);

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT to_regclass('mail_archiver.\"ArchivedEmails\"') IS NOT NULL";
            command.CommandTimeout = TimeoutSeconds;

            var result = await command.ExecuteScalarAsync(timeout.Token);
            return result is true
                ? HealthCheckResult.Healthy("PostgreSQL is reachable and the archive schema is present.")
                : HealthCheckResult.Unhealthy("PostgreSQL is reachable but the archive schema is missing.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy(
                $"PostgreSQL did not answer within {TimeoutSeconds} seconds.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL is not reachable.", ex);
        }
    }
}

using System.Text.Json;
using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Models.ViewModels;
using MailArchiver.Services.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace MailArchiver.Services
{
    /// <summary>
    /// Autarker Background-Service fuer vorberechnete Dashboard-Statistiken.
    /// Auf Installationen mit mehreren Millionen Mails dauern die Dashboard-
    /// Aggregate (COUNT ueber alle Mails/Anhaenge, GROUP BY ueber alle Absender,
    /// 12-Monats-Histogramm) rund zehn Sekunden. Dieser Service berechnet sie
    /// periodisch ausserhalb des Request-Pfads und speichert sie in der
    /// DashboardStatsCache-Tabelle; der Dashboard-Aufruf liest nur die
    /// vorbereitete Zeile. Unabhaengig von DatabaseMaintenance:Enabled.
    /// </summary>
    public class DashboardStatsRefreshService : BackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<DashboardStatsRefreshService> _logger;
        private readonly IOptions<DashboardOptions> _dashboardOptions;
        private readonly IConfiguration _configuration;

        /// <summary>Cache-Key des admin-Scopes; User-Dashboards bleiben live.</summary>
        internal const string AdminCacheKey = "admin";

        /// <summary>Wartezeit nach dem Start, damit Migration/Sync zuerst ruhig anlaufen.</summary>
        private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);

        public DashboardStatsRefreshService(
            IServiceScopeFactory serviceScopeFactory,
            ILogger<DashboardStatsRefreshService> logger,
            IOptions<DashboardOptions> dashboardOptions,
            IConfiguration configuration)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
            _dashboardOptions = dashboardOptions;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var intervalMinutes = ResolveIntervalMinutes();

            if (intervalMinutes <= 0)
            {
                _logger.LogInformation("Dashboard Stats Refresh Service is disabled (Dashboard:RefreshIntervalMinutes = 0)");
                return;
            }

            _logger.LogInformation("Dashboard Stats Refresh Service is starting (interval: {Minutes} minutes)", intervalMinutes);

            // Warmup: erste Zeile frueh, aber nach den Startmigrationen und dem
            // initialen Sync-Anlauf berechnen, damit der erste Besucher sie vorfindet.
            try
            {
                await Task.Delay(StartupDelay, stoppingToken);

                while (!stoppingToken.IsCancellationRequested)
                {
                    var success = await RefreshAdminStatisticsAsync(stoppingToken);
                    if (success)
                        break;

                    // Noch keine Tabelle/Migration oder Datenbank nicht bereit:
                    // kurz warten und erneut versuchen.
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during dashboard stats warmup: {Message}", ex.Message);
            }

            if (stoppingToken.IsCancellationRequested)
                return;

            // Periodische Aktualisierung
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);

                    if (stoppingToken.IsCancellationRequested)
                        break;

                    intervalMinutes = ResolveIntervalMinutes();
                    if (intervalMinutes <= 0)
                    {
                        _logger.LogInformation("Dashboard Stats Refresh Service stopped: refresh interval set to 0");
                        return;
                    }

                    await RefreshAdminStatisticsAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in dashboard stats refresh loop: {Message}", ex.Message);
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
            }

            _logger.LogInformation("Dashboard Stats Refresh Service has stopped");
        }

        /// <summary>
        /// Liest das Intervall aus der Konfiguration. Auch zur Laufzeit relevant,
        /// damit eine geaenderte appsettings.json beim naechsten Zyklus greift.
        /// </summary>
        private int ResolveIntervalMinutes()
        {
            var configured = _configuration.GetValue<int?>("Dashboard:RefreshIntervalMinutes");
            return configured ?? _dashboardOptions.Value.RefreshIntervalMinutes;
        }

        /// <summary>
        /// Berechnet die admin-Statistiken und upsertet sie in die Cache-Tabelle.
        /// Liefert false, wenn die Zieltabelle (noch) nicht existiert.
        /// </summary>
        internal async Task<bool> RefreshAdminStatisticsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();

                var connectionString = context.Database.GetConnectionString();
                if (string.IsNullOrEmpty(connectionString))
                    return false;

                // Vor jedem Lauf pruefen, ob die Cache-Tabelle existiert; direkt nach
                // dem Start laeuft das Migrieren teils parallel dazu (retry bis bereit).
                await using (var checkConnection = new NpgsqlConnection(connectionString))
                {
                    await checkConnection.OpenAsync(cancellationToken);
                    if (!await TableExistsAsync(checkConnection, cancellationToken))
                        return false;
                }

                var startTime = DateTime.UtcNow;

                var model = await Task.Run(() => BuildAdminStatistics(context), cancellationToken);

                var databaseSizeBytes = await GetDatabaseSizeAsync(context);

                var cache = new DashboardStatsCache
                {
                    Key = AdminCacheKey,
                    TotalEmails = model.TotalEmails,
                    TotalAttachments = model.TotalAttachments,
                    TotalAccounts = model.TotalAccounts,
                    TotalDatabaseSizeBytes = databaseSizeBytes,
                    TopSendersJson = Serialize(model.TopSenders),
                    EmailsByMonthJson = Serialize(model.EmailsByMonth),
                    EmailsPerAccountJson = Serialize(model.EmailsPerAccount),
                    ComputedAtUtc = DateTime.UtcNow
                };

                // Update und Insert in einem Roundtrip: die Zeile existiert
                // nach dem ersten Lauf immer, Updaten ist der Normalfall.
                var existing = await context.DashboardStatsCaches
                    .FirstOrDefaultAsync(c => c.Key == AdminCacheKey, cancellationToken);

                if (existing != null)
                {
                    existing.TotalEmails = cache.TotalEmails;
                    existing.TotalAttachments = cache.TotalAttachments;
                    existing.TotalAccounts = cache.TotalAccounts;
                    existing.TotalDatabaseSizeBytes = cache.TotalDatabaseSizeBytes;
                    existing.TopSendersJson = cache.TopSendersJson;
                    existing.EmailsByMonthJson = cache.EmailsByMonthJson;
                    existing.EmailsPerAccountJson = cache.EmailsPerAccountJson;
                    existing.ComputedAtUtc = cache.ComputedAtUtc;
                }
                else
                {
                    context.DashboardStatsCaches.Add(cache);
                }

                await context.SaveChangesAsync(cancellationToken);

                var duration = DateTime.UtcNow - startTime;
                _logger.LogInformation("Dashboard statistics computed and cached in {Duration:F1} seconds (interval: {Minutes} minutes)",
                    duration.TotalSeconds, ResolveIntervalMinutes());

                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing dashboard statistics: {Message}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Dieselben Aggregate wie das admin-Dashboard, nur hier im Hintergrund.
        /// RecentEmails bleiben bewusst draussen: zehn indexgestuetzte Zeilen liest
        /// der Request-Pfad live, im Snapshot waeren die "neuesten Mails" alt.
        /// Das Account-Panel ohne Issue-Flag zu speichern ist hier genau richtig:
        /// die Reihenfolge wird ohnehin pro Request neu angewandt (ApplyPanelOrder).
        /// </summary>
        private static DashboardViewModel BuildAdminStatistics(MailArchiverDbContext context)
        {
            return new DashboardViewModel
            {
                TotalEmails = context.ArchivedEmails.Count(),
                TotalAccounts = context.MailAccounts.Count(),
                TotalAttachments = context.EmailAttachments.Count(),
                EmailsPerAccount = EmailCoreService.BuildAccountPanel(context.MailAccounts, _ => false),
                EmailsByMonth = EmailCoreService.BuildEmailsByMonth(context.ArchivedEmails),
                TopSenders = context.ArchivedEmails
                    .Where(e => !e.IsOutgoing)
                    .GroupBy(e => e.From)
                    .Select(g => new EmailCountByAddress
                    {
                        EmailAddress = g.Key,
                        Count = g.Count()
                    })
                    .OrderByDescending(e => e.Count)
                    .Take(10)
                    .ToList()
            };
        }

        private static async Task<long> GetDatabaseSizeAsync(MailArchiverDbContext context)
        {
            await using var connection = new NpgsqlConnection(context.Database.GetConnectionString());
            await connection.OpenAsync();

            await using var command = new NpgsqlCommand("SELECT pg_database_size(current_database())", connection);
            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt64(result);
        }

        private static string Serialize<T>(List<T> list)
        {
            return JsonSerializer.Serialize(list ?? new List<T>());
        }

        private static async Task<bool> TableExistsAsync(NpgsqlConnection connection, CancellationToken token)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT EXISTS (
                    SELECT 1 FROM information_schema.tables
                    WHERE table_schema = 'mail_archiver' AND table_name = 'DashboardStatsCache'
                );";
            var result = await command.ExecuteScalarAsync(token);
            return result != null && (bool)result;
        }
    }
}
using Npgsql;
using MailArchiver.Models;

namespace MailArchiver.Services
{
    public class DatabaseMaintenanceService : BackgroundService, IDatabaseMaintenanceService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<DatabaseMaintenanceService> _logger;
        private readonly IConfiguration _configuration;
        private DateTime? _lastMaintenanceTime;
        private DateTime? _nextScheduledMaintenanceTime;

        public DateTime? LastMaintenanceTime => _lastMaintenanceTime;
        public DateTime? NextScheduledMaintenanceTime => _nextScheduledMaintenanceTime;

        public DatabaseMaintenanceService(
            IServiceScopeFactory serviceScopeFactory,
            ILogger<DatabaseMaintenanceService> logger,
            IConfiguration configuration)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var enabled = _configuration.GetValue<bool>("DatabaseMaintenance:Enabled", false);
            
            if (!enabled)
            {
                _logger.LogInformation("Database Maintenance Service is disabled in configuration");
                return;
            }

            _logger.LogInformation("Database Maintenance Service is starting...");

            var dailyExecutionTime = _configuration.GetValue<string>("DatabaseMaintenance:DailyExecutionTime", "02:00");
            
            if (!TimeSpan.TryParse(dailyExecutionTime, out TimeSpan executionTime))
            {
                _logger.LogError("Invalid DailyExecutionTime format: {Time}. Expected format: HH:mm. Service will not run.", dailyExecutionTime);
                return;
            }

            _logger.LogInformation("Database Maintenance scheduled daily at {Time}", dailyExecutionTime);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var now = DateTime.Now;
                    var nextRun = CalculateNextRunTime(executionTime);
                    _nextScheduledMaintenanceTime = nextRun;

                    var delay = nextRun - now;
                    
                    _logger.LogInformation("Next database maintenance scheduled for {NextRun} (in {Hours:F1} hours)", 
                        nextRun, delay.TotalHours);

                    // Wait until the next scheduled time
                    await Task.Delay(delay, stoppingToken);

                    if (stoppingToken.IsCancellationRequested)
                        break;

                    // Perform the maintenance
                    _logger.LogInformation("Starting scheduled database maintenance at {Time}", DateTime.Now);
                    var success = await PerformMaintenanceAsync();
                    
                    if (success)
                    {
                        _logger.LogInformation("Database maintenance completed successfully at {Time}", DateTime.Now);
                    }
                    else
                    {
                        _logger.LogWarning("Database maintenance completed with errors at {Time}", DateTime.Now);
                    }

                    // Small delay to prevent immediate re-execution if calculating next run time has issues
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Database Maintenance Service is stopping (operation cancelled)");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Database Maintenance Service: {Message}", ex.Message);
                    // Wait 1 hour before retrying after an error
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
            }

            _logger.LogInformation("Database Maintenance Service has stopped");
        }

        public async Task<bool> PerformMaintenanceAsync()
        {
            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                
                if (string.IsNullOrEmpty(connectionString))
                {
                    _logger.LogError("Database connection string not found in configuration");
                    return false;
                }

                _logger.LogInformation("Connecting to PostgreSQL database for maintenance...");

                using (var connection = new NpgsqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    
                    _logger.LogInformation("Executing VACUUM ANALYZE command...");
                    
                    var startTime = DateTime.UtcNow;
                    
                    using (var command = new NpgsqlCommand("VACUUM ANALYZE;", connection))
                    {
                        // Set a longer timeout for VACUUM operations (default: 30 minutes)
                        var maintenanceTimeout = _configuration.GetValue<int>("DatabaseMaintenance:TimeoutMinutes", 30);
                        command.CommandTimeout = maintenanceTimeout * 60;
                        
                        await command.ExecuteNonQueryAsync();
                    }
                    
                    var duration = DateTime.UtcNow - startTime;
                    _lastMaintenanceTime = DateTime.UtcNow;
                    
                    _logger.LogInformation("VACUUM ANALYZE completed successfully in {Duration:F1} seconds", duration.TotalSeconds);

                    // Log the maintenance action using scoped service
                    using (var scope = _serviceScopeFactory.CreateScope())
                    {
                        var accessLogService = scope.ServiceProvider.GetRequiredService<IAccessLogService>();
                        await accessLogService.LogAccessAsync(
                            "SYSTEM",
                            AccessLogType.DatabaseMaintenance,
                            searchParameters: $"Database maintenance completed in {duration.TotalSeconds:F1} seconds"
                        );
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing database maintenance: {Message}", ex.Message);
                
                // Log the error using scoped service
                try
                {
                    using (var scope = _serviceScopeFactory.CreateScope())
                    {
                        var accessLogService = scope.ServiceProvider.GetRequiredService<IAccessLogService>();
                        await accessLogService.LogAccessAsync(
                            "SYSTEM",
                            AccessLogType.DatabaseMaintenance,
                            searchParameters: $"Database maintenance failed: {ex.Message}"
                        );
                    }
                }
                catch (Exception logEx)
                {
                    _logger.LogError(logEx, "Failed to log maintenance error to access log");
                }
                
                return false;
            }
        }

        private DateTime CalculateNextRunTime(TimeSpan executionTime)
        {
            var now = DateTime.Now;
            var todayAtExecutionTime = now.Date.Add(executionTime);

            // If today's execution time has passed, schedule for tomorrow
            if (now >= todayAtExecutionTime)
            {
                return todayAtExecutionTime.AddDays(1);
            }

            return todayAtExecutionTime;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            var enabled = _configuration.GetValue<bool>("DatabaseMaintenance:Enabled", false);
            
            if (enabled)
            {
                _logger.LogInformation("Database Maintenance Service is starting.");
            }
            
            return base.StartAsync(cancellationToken);
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Database Maintenance Service is stopping.");
            return base.StopAsync(cancellationToken);
        }
    }
}

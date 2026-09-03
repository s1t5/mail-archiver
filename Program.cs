using MailArchiver.Auth.Extensions;
using MailArchiver.Auth.Options;
using MailArchiver.Auth.Services;
using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services;
using MailArchiver.Services.Providers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// Helper method to parse SameSite mode from string
static SameSiteMode ParseSameSiteMode(string? value)
{
    return value?.ToLowerInvariant() switch
    {
        "strict" => SameSiteMode.Strict,
        "none" => SameSiteMode.None,
        _ => SameSiteMode.Lax // Default to Lax for better cross-site navigation support
    };
}

// Helper method to ensure __EFMigrationsHistory table exists
async static Task EnsureMigrationsHistoryTableExists(MailArchiverDbContext context, IServiceProvider services)
{
    var connection = context.Database.GetDbConnection();
    
    // Check if connection is already open
    if (connection.State != System.Data.ConnectionState.Open)
    {
        await connection.OpenAsync();
    }
    
    var command = connection.CreateCommand();
    command.CommandText = @"
        SELECT EXISTS (
            SELECT 1 
            FROM information_schema.tables 
            WHERE table_name = '__EFMigrationsHistory'
        );";
    
    var result = await command.ExecuteScalarAsync();
    var tableExists = result != null && (bool)result;
    
    if (!tableExists)
    {
        // Create the migrations history table if it doesn't exist
        var createTableCommand = connection.CreateCommand();
        createTableCommand.CommandText = @"
            CREATE TABLE IF NOT EXISTS ""__EFMigrationsHistory"" (
                ""MigrationId"" character varying(150) NOT NULL,
                ""ProductVersion"" character varying(32) NOT NULL,
                CONSTRAINT ""PK___EFMigrationsHistory"" PRIMARY KEY (""MigrationId"")
            );";
        await createTableCommand.ExecuteNonQueryAsync();
        
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("__EFMigrationsHistory table created");
    }
}

var builder = WebApplication.CreateBuilder(args);

// Configure Forwarded Headers for reverse proxy support
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | 
                              Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedHost | 
                              Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Check if authentication is explicitly disabled in appsettings.json
var authEnabled = builder.Configuration.GetSection("Authentication:Enabled").Value;
if (authEnabled != null && authEnabled.Equals("false", StringComparison.OrdinalIgnoreCase))
{
    // Create a logger to log the error message
    var logger = builder.Services.BuildServiceProvider().GetRequiredService<ILogger<Program>>();
    logger.LogError("Authentication is now mandatory and must be enabled. Please remove the 'Enabled' property from the 'Authentication' section in appsettings.json or set it to 'true' and define admin credentials to access the application.");
    logger.LogError("For more information, please refer to the documentation ( https://github.com/s1t5/mail-archiver/blob/main/doc/Setup.md ) on how to set up username and password using environment variables.");
    Environment.Exit(1);
}

// Check if authentication password is set and not empty
var authPassword = builder.Configuration.GetSection("Authentication:Password").Value;
if (string.IsNullOrWhiteSpace(authPassword))
{
    // Create a logger to log the error message
    var logger = builder.Services.BuildServiceProvider().GetRequiredService<ILogger<Program>>();
    logger.LogError("Authentication password must be set and cannot be empty. Please define a valid password in the 'Authentication' section in appsettings.json or using environment variables.");
    logger.LogError("For more information, please refer to the documentation ( https://github.com/s1t5/mail-archiver/blob/main/doc/Setup.md ) on how to set up username and password using environment variables.");
    Environment.Exit(1);
}

// Add Authentication Options
builder.Services.Configure<AuthenticationOptions>(
    builder.Configuration.GetSection(AuthenticationOptions.Authentication));

// Add OAuth Options
builder.Services.Configure<OAuthOptions>(
    builder.Configuration.GetSection(OAuthOptions.OAuth));

// Add Batch Restore Options
builder.Services.Configure<BatchRestoreOptions>(
    builder.Configuration.GetSection(BatchRestoreOptions.BatchRestore));

// Add Batch Operation Options
builder.Services.Configure<BatchOperationOptions>(
    builder.Configuration.GetSection(BatchOperationOptions.BatchOperation));

// Add Tenant Management Options
builder.Services.Configure<OffloadOptions>(
    builder.Configuration.GetSection(OffloadOptions.Offload));

builder.Services.Configure<TenantManagementOptions>(
    builder.Configuration.GetSection(TenantManagementOptions.TenantManagement));

// Add Mail Sync Options
builder.Services.Configure<MailSyncOptions>(
    builder.Configuration.GetSection(MailSyncOptions.MailSync));

// Add Upload Options
builder.Services.Configure<UploadOptions>(
    builder.Configuration.GetSection(UploadOptions.Upload));

// Add Local Import Options
builder.Services.Configure<LocalImportOptions>(
    builder.Configuration.GetSection(LocalImportOptions.LocalImport));

// Add Selection Options
builder.Services.Configure<SelectionOptions>(
    builder.Configuration.GetSection("Selection"));

// Add View Options
builder.Services.Configure<ViewOptions>(
    builder.Configuration.GetSection("View"));

// Add TimeZone Options
builder.Services.Configure<TimeZoneOptions>(
    builder.Configuration.GetSection("TimeZone"));

// Add Bandwidth Tracking Options
builder.Services.Configure<BandwidthTrackingOptions>(
    builder.Configuration.GetSection(BandwidthTrackingOptions.BandwidthTracking));

// Add ReleaseNotes Options
builder.Services.Configure<ReleaseNotesOptions>(
    builder.Configuration.GetSection(ReleaseNotesOptions.ReleaseNotes));

// Add Deletion Policy Options
builder.Services.Configure<DeletionPolicyOptions>(
    builder.Configuration.GetSection(DeletionPolicyOptions.DeletionPolicy));

// ===== Read-only REST API (v1) — kept in one contiguous block to minimize
// upstream merge churn. Disabled by default via Api:Enabled. =====
builder.Services.Configure<MailArchiver.Models.ApiOptions>(
    builder.Configuration.GetSection(MailArchiver.Models.ApiOptions.Api));
builder.Services.AddScoped<MailArchiver.Services.IApiKeyService, MailArchiver.Services.ApiKeyService>();
// RFC 7807 problem+json for API errors (used by the /api exception handler and
// the auth middleware's 401 responses).
builder.Services.AddProblemDetails();
// OpenAPI document "v1" scoped to the /api/v1 endpoints, with the bearer scheme.
builder.Services.AddOpenApi("v1", openApiOptions =>
{
    openApiOptions.ShouldInclude = description =>
        description.RelativePath?.StartsWith("api/v1", StringComparison.OrdinalIgnoreCase) ?? false;
    openApiOptions.AddDocumentTransformer<MailArchiver.Models.Api.OpenApi.BearerSecuritySchemeTransformer>();
});
// ===== End read-only REST API block =====

// ===== MCP (Model Context Protocol) server — mirrors the REST API for AI agent
// access. Disabled by default via Mcp:Enabled. Uses the same API keys (/mcp is
// gated by ApiKeyAuthenticationHandler via AuthenticationMiddleware). =====
builder.Services.Configure<MailArchiver.Models.McpOptions>(
    builder.Configuration.GetSection(MailArchiver.Models.McpOptions.Mcp));
builder.Services.AddHttpContextAccessor();
var mcpEnabled = builder.Configuration.GetSection(MailArchiver.Models.McpOptions.Mcp)
    .Get<MailArchiver.Models.McpOptions>()?.Enabled ?? false;
if (mcpEnabled)
{
    builder.Services.AddMcpServer()
        .WithHttpTransport(options => options.Stateless = true)
        .WithTools<MailArchiver.Mcp.Tools.AccountsMcpTool>()
        .WithTools<MailArchiver.Mcp.Tools.EmailsMcpTool>();
}
// ===== End MCP server block =====

// Add DateTimeHelper
builder.Services.AddScoped<MailArchiver.Utilities.DateTimeHelper>();

// Add HTTP Client factory (used by VersionUpdateService for GitHub API calls)
builder.Services.AddHttpClient("GitHubReleases");
builder.Services.AddHttpClient("MsaOAuth");

// Register CSV import options for bulk IMAP account import
builder.Services.Configure<CsvImportOptions>(builder.Configuration.GetSection(CsvImportOptions.CsvImport));

// Register MSA OAuth options and service for personal Microsoft accounts
builder.Services.Configure<MsaOAuthOptions>(builder.Configuration.GetSection(MsaOAuthOptions.SectionName));
builder.Services.AddScoped<MailArchiver.Services.IMsaOAuthService, MailArchiver.Services.MsaOAuthService>();

// Add Session support
builder.Services.AddDistributedMemoryCache();

// Get authentication options for SameSite configuration
var authOptionsConfig = builder.Configuration.GetSection(AuthenticationOptions.Authentication).Get<AuthenticationOptions>() ?? new AuthenticationOptions();
var cookieSameSiteMode = ParseSameSiteMode(authOptionsConfig.CookieSameSite);

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = cookieSameSiteMode;
});

// Configure Anti-forgery (CSRF) cookies with same SameSite policy
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = cookieSameSiteMode;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

// Add Data Protection with persistent key storage
var dataProtectionPath = builder.Configuration.GetValue<string>("DataProtection:KeyPath") ?? "/app/DataProtection-Keys";
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath))
    .SetApplicationName("MailArchiver");

// Add Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    // Login Attempt Rate Limiting: 5 attempts per 10 minutes per IP
    options.AddPolicy("LoginAttempts", httpContext =>
    {
        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var partitionKey = $"login-{clientIp}";
        
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(10),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });
    
    // 2FA Verification Rate Limiting: 5 attempts per 15 minutes per IP/User
    options.AddPolicy("TwoFactorVerify", httpContext =>
    {
        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var username = httpContext.Session.GetString("TwoFactorUsername") ?? "anonymous";
        var partitionKey = $"2fa-{clientIp}-{username}";
        
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(15),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });
    
    // Global Rate Limiting: 100 requests per minute per IP for other endpoints
    options.AddPolicy("Global", httpContext =>
    {
        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        
        return RateLimitPartition.GetFixedWindowLimiter(
            clientIp,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    // Read-only REST API rate limiting: fixed window per API key (prefix),
    // falling back to client IP. Budget from Api:RateLimitPerMinute (default 120).
    var apiOptionsForRateLimit = builder.Configuration.GetSection(MailArchiver.Models.ApiOptions.Api)
        .Get<MailArchiver.Models.ApiOptions>() ?? new MailArchiver.Models.ApiOptions();
    options.AddPolicy("Api", httpContext =>
    {
        string partitionKey;
        string? authHeader = httpContext.Request.Headers.Authorization;
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.Ordinal))
        {
            var token = authHeader.Substring("Bearer ".Length).Trim();
            // Partition by the non-secret key prefix, never the full key.
            partitionKey = "apikey-" + (token.Length >= 11 ? token.Substring(0, 11) : token);
        }
        else
        {
            partitionKey = "apiip-" + (httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
        }

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = apiOptionsForRateLimit.RateLimitPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    // MCP server rate limiting: same scheme as the REST API policy above — fixed
    // window per API key (non-secret prefix) or client IP fallback. Budget from
    // Mcp:RateLimitPerMinute (default 120). Applied to the /mcp endpoint via
    // RequireRateLimiting("Mcp") at the MapMcp call below — MCP tools are not
    // MVC controllers and so cannot use [EnableRateLimiting] attributes.
    var mcpOptionsForRateLimit = builder.Configuration.GetSection(MailArchiver.Models.McpOptions.Mcp)
        .Get<MailArchiver.Models.McpOptions>() ?? new MailArchiver.Models.McpOptions();
    options.AddPolicy("Mcp", httpContext =>
    {
        string partitionKey;
        string? authHeader = httpContext.Request.Headers.Authorization;
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.Ordinal))
        {
            var token = authHeader.Substring("Bearer ".Length).Trim();
            partitionKey = "mcpkey-" + (token.Length >= 11 ? token.Substring(0, 11) : token);
        }
        else
        {
            partitionKey = "mcpip-" + (httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
        }

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = mcpOptionsForRateLimit.RateLimitPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    // Rejection response
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = 429;
        
        if (context.Lease.TryGetMetadata(System.Threading.RateLimiting.MetadataName.RetryAfter, out var retryAfter))
        {
            var retryAfterSeconds = retryAfter is TimeSpan ts ? ts.TotalSeconds : 0;
            context.HttpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString();
        }
        
        // Redirect to blocked page for login and 2FA endpoints
        var path = context.HttpContext.Request.Path.Value?.ToLowerInvariant() ?? "";
        if (path.Contains("/auth/login") || path.Contains("/twofactor/verify"))
        {
            context.HttpContext.Response.Redirect("/Auth/Blocked");
        }
        else
        {
            // Get localizer for rate limit message
            var serviceProvider = context.HttpContext.RequestServices;
            var localizer = serviceProvider.GetService<Microsoft.Extensions.Localization.IStringLocalizer<MailArchiver.SharedResource>>();
            var message = localizer?["RateLimitExceeded"] ?? "Rate limit exceeded. Please try again later.";
            
            await context.HttpContext.Response.WriteAsync(message, cancellationToken: token);
        }
    };
});

// Add Authentication
builder.AddAuth();

// Set global encoding to UTF-8
System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

// Attachment deduplication interceptor (dedupes attachment payloads on SaveChanges)
builder.Services.AddSingleton<MailArchiver.Services.AttachmentDeduplicationInterceptor>();

// PostgreSQL-Datenbankkontext hinzufügen
builder.Services.AddDbContext<MailArchiverDbContext>((serviceProvider, options) =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    
    options.UseNpgsql(
        connectionString,
        npgsqlOptions => {
            npgsqlOptions.CommandTimeout(
                    builder.Configuration.GetValue<int>("Npgsql:CommandTimeout", 60)
            );
        }
    )
    .ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
    .AddInterceptors(serviceProvider.GetRequiredService<MailArchiver.Services.AttachmentDeduplicationInterceptor>());
    
    // Enable sensitive data logging for debugging (remove in production)
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
    }
});


// Services hinzufügen

// Graph API services (refactored from monolithic GraphEmailService)
builder.Services.AddSingleton<MailArchiver.Services.Providers.Graph.GraphAuthClientFactory>();
builder.Services.AddScoped<MailArchiver.Services.Providers.Graph.IGraphFolderService, MailArchiver.Services.Providers.Graph.GraphFolderService>();
builder.Services.AddScoped<MailArchiver.Services.Providers.Graph.GraphMailArchiver>();
builder.Services.AddScoped<MailArchiver.Services.Providers.Graph.GraphMailRestorer>();
builder.Services.AddScoped<MailArchiver.Services.Providers.Graph.GraphMailSyncService>();

// GraphEmailService facade – implements both IGraphEmailService and IProviderEmailService
builder.Services.AddScoped<IGraphEmailService, GraphEmailService>();
builder.Services.AddScoped<MailArchiver.Services.Providers.IProviderEmailService>(provider => 
    provider.GetRequiredService<IGraphEmailService>() as MailArchiver.Services.Providers.IProviderEmailService);
builder.Services.AddScoped<IAuthenticationService, CookieAuthenticationService>();
builder.Services.AddScoped<OAuthAuthenticationService>();
builder.Services.AddScoped<IUserService, UserService>();
// Single source of truth for per-user account-access scoping, shared by the
// REST API (ApiControllerBase) and the MCP server (McpToolBase).
builder.Services.AddScoped<IAccountAccessResolver, AccountAccessResolver>();
builder.Services.AddSingleton<ISyncJobService, SyncJobService>(); // NEUE SERVICE

// Register BatchRestoreService as singleton and hosted service - MUST be the same instance
builder.Services.AddSingleton<BatchRestoreService>();
builder.Services.AddSingleton<IBatchRestoreService>(provider => provider.GetRequiredService<BatchRestoreService>());
builder.Services.AddHostedService<BatchRestoreService>(provider => provider.GetRequiredService<BatchRestoreService>());

// MBox import services (refactored from monolithic MBoxImportService)
builder.Services.AddScoped<MailArchiver.Services.Providers.MBox.MBoxStreamProcessor>();

// Register MBoxImportService as singleton and hosted service - MUST be the same instance
builder.Services.AddSingleton<MBoxImportService>();
builder.Services.AddSingleton<IMBoxImportService>(provider => provider.GetRequiredService<MBoxImportService>());
builder.Services.AddHostedService<MBoxImportService>(provider => provider.GetRequiredService<MBoxImportService>());

// EML import services (refactored from monolithic EmlImportService)
builder.Services.AddScoped<MailArchiver.Services.Providers.Eml.EmlMailCleaner>();
builder.Services.AddScoped<MailArchiver.Services.Providers.Eml.EmlAttachmentCollector>();
builder.Services.AddScoped<MailArchiver.Services.Shared.MailImporter>();
builder.Services.AddScoped<MailArchiver.Services.Providers.Eml.EmlTruncatedContentSaver>();

// Register EmlImportService as singleton and hosted service - MUST be the same instance
builder.Services.AddSingleton<EmlImportService>();
builder.Services.AddSingleton<IEmlImportService>(provider => provider.GetRequiredService<EmlImportService>());
builder.Services.AddHostedService<EmlImportService>(provider => provider.GetRequiredService<EmlImportService>());

// Register ExportService as singleton and hosted service - MUST be the same instance
builder.Services.AddSingleton<ExportService>();
builder.Services.AddSingleton<IExportService>(provider => provider.GetRequiredService<ExportService>());
builder.Services.AddHostedService<ExportService>(provider => provider.GetRequiredService<ExportService>());

// Audit data export (tabular mass data packages for external audit tools)
builder.Services.Configure<AuditExportOptions>(builder.Configuration.GetSection(AuditExportOptions.SectionName));

// Register AuditExportService as singleton and hosted service - MUST be the same instance
builder.Services.AddSingleton<AuditExportService>();
builder.Services.AddSingleton<IAuditExportService>(provider => provider.GetRequiredService<AuditExportService>());
builder.Services.AddHostedService<AuditExportService>(provider => provider.GetRequiredService<AuditExportService>());

// Register SelectedEmailsExportService as singleton and hosted service - MUST be the same instance
builder.Services.AddSingleton<SelectedEmailsExportService>();
builder.Services.AddSingleton<ISelectedEmailsExportService>(provider => provider.GetRequiredService<SelectedEmailsExportService>());
builder.Services.AddHostedService<SelectedEmailsExportService>(provider => provider.GetRequiredService<SelectedEmailsExportService>());

// Register MailAccountDeletionService as singleton and hosted service - MUST be the same instance
builder.Services.AddSingleton<MailAccountDeletionService>();
builder.Services.AddSingleton<IMailAccountDeletionService>(provider => provider.GetRequiredService<MailAccountDeletionService>());
builder.Services.AddHostedService<MailAccountDeletionService>(provider => provider.GetRequiredService<MailAccountDeletionService>());

// Register EmailDeletionService as singleton and hosted service - MUST be the same instance
builder.Services.AddSingleton<EmailDeletionService>();
builder.Services.AddSingleton<IEmailDeletionService>(provider => provider.GetRequiredService<EmailDeletionService>());
builder.Services.AddHostedService<EmailDeletionService>(provider => provider.GetRequiredService<EmailDeletionService>());

// Register DeletionPolicyApplicationService as singleton and hosted service - MUST be the same instance
builder.Services.AddSingleton<DeletionPolicyApplicationService>();
builder.Services.AddHostedService<DeletionPolicyApplicationService>(provider => provider.GetRequiredService<DeletionPolicyApplicationService>());

builder.Services.AddHostedService<MailSyncBackgroundService>();

// Register DatabaseMaintenanceService as singleton and hosted service - MUST be the same instance
builder.Services.AddSingleton<DatabaseMaintenanceService>();
builder.Services.AddSingleton<IDatabaseMaintenanceService>(provider => provider.GetRequiredService<DatabaseMaintenanceService>());
builder.Services.AddHostedService<DatabaseMaintenanceService>(provider => provider.GetRequiredService<DatabaseMaintenanceService>());

// Register the resumable attachment deduplication background migration (existing data)
builder.Services.AddHostedService<AttachmentDeduplicationBackgroundService>();

// Register AccountStorageService (scoped) and the autark refresh background service
// (backfill on startup + daily full refresh, independent of DatabaseMaintenance:Enabled)
builder.Services.AddScoped<IAccountStorageService, AccountStorageService>();
builder.Services.AddHostedService<AccountStorageRefreshService>();

// Register AccessLogService
builder.Services.AddScoped<IAccessLogService, AccessLogService>();


// Register VersionUpdateService (release notes / changelog splash screen)
builder.Services.AddSingleton<IVersionUpdateService, VersionUpdateService>();

// Register BandwidthService for rate limit management
builder.Services.AddScoped<IBandwidthService, BandwidthService>();

// ====================
// NEW: Provider-based Architecture Services
// ====================

// IMAP services (refactored from monolithic ImapEmailService)
builder.Services.AddScoped<MailArchiver.Services.Providers.Imap.ImapConnectionFactory>();
builder.Services.AddScoped<MailArchiver.Services.Providers.Imap.IImapFolderService, MailArchiver.Services.Providers.Imap.ImapFolderService>();
builder.Services.AddScoped<MailArchiver.Services.Providers.Imap.ImapMailRestorer>();
builder.Services.AddScoped<MailArchiver.Services.Providers.Imap.ImapMailSyncService>();

builder.Services.AddScoped<MailArchiver.Services.Core.EmailCoreService>();
builder.Services.AddScoped<MailArchiver.Services.Providers.ImapEmailService>();
builder.Services.AddScoped<MailArchiver.Services.Providers.ImportEmailService>();
builder.Services.AddScoped<MailArchiver.Services.Factories.ProviderEmailServiceFactory>();

// In-memory cache for dashboard statistics (TTL via Dashboard:CacheSeconds)
builder.Services.AddMemoryCache();
builder.Services.Configure<DashboardOptions>(
    builder.Configuration.GetSection(DashboardOptions.SectionName));

// Add Localization
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
// Configure Form Options for large file uploads
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    var uploadOptions = builder.Configuration.GetSection(UploadOptions.Upload).Get<UploadOptions>() ?? new UploadOptions();
    
    options.ValueCountLimit = 100_000; // Allow up to 100k form entries (e.g. batch email IDs) to prevent 400 Bad Request
    options.MultipartBodyLengthLimit = uploadOptions.MaxFileSizeBytes;
    options.ValueLengthLimit = (int)Math.Min(uploadOptions.MaxFileSizeBytes, int.MaxValue);
    options.MultipartHeadersLengthLimit = (int)Math.Min(uploadOptions.MaxFileSizeBytes, int.MaxValue);
    options.MemoryBufferThreshold = int.MaxValue;
    options.BufferBody = false; // Stream large files directly to disk
});

// MVC hinzufügen
builder.Services.AddControllersWithViews(options =>
{
    // Add global filter for password change requirement
    options.Filters.Add<MailArchiver.Attributes.PasswordChangeRequiredAttribute>();
})
    .AddViewLocalization();

builder.Services.Configure<BatchRestoreOptions>(
    builder.Configuration.GetSection(BatchRestoreOptions.BatchRestore));


// Kestrel-Server-Limits konfigurieren - using configuration values
builder.WebHost.ConfigureKestrel((context, options) =>
{
    var uploadOptions = context.Configuration.GetSection(UploadOptions.Upload).Get<UploadOptions>() ?? new UploadOptions();
    
    options.Limits.MaxRequestBodySize = long.MaxValue;
    options.Limits.KeepAliveTimeout = TimeSpan.FromHours(uploadOptions.KeepAliveTimeoutHours);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromHours(uploadOptions.RequestHeadersTimeoutHours);
});

var app = builder.Build();

// Handle CLI commands: S3 disaster recovery and local import
var cliArgs = Environment.GetCommandLineArgs();
if (cliArgs.Any(a => a == "--import-mbox" || a == "--import-eml"))
{
    using var cliScope = app.Services.CreateScope();
    var cliServices = cliScope.ServiceProvider;
    var cliLogger = cliServices.GetRequiredService<ILogger<Program>>();
    
    try
    {      
        // === Local File Import Commands ===
        if (cliArgs.Contains("--import-mbox") || cliArgs.Contains("--import-eml"))
        {
            var isMbox = cliArgs.Contains("--import-mbox");
            var formatLabel = isMbox ? "MBox" : "EML";
            cliLogger.LogInformation("Starting local {Format} import...", formatLabel);
            
            // Parse required arguments
            var filePathIndex = Array.IndexOf(cliArgs, "--file");
            var accountIdIndex = Array.IndexOf(cliArgs, "--account-id");
            var folderIndex = Array.IndexOf(cliArgs, "--folder");
            
            if (filePathIndex < 0 || filePathIndex + 1 >= cliArgs.Length)
            {
                Console.WriteLine($"ERROR: --file <path> is required for {formatLabel} import");
                Environment.Exit(1);
            }
            if (accountIdIndex < 0 || accountIdIndex + 1 >= cliArgs.Length)
            {
                Console.WriteLine($"ERROR: --account-id <id> is required for {formatLabel} import");
                Environment.Exit(1);
            }
            
            var filePath = cliArgs[filePathIndex + 1];
            var accountIdStr = cliArgs[accountIdIndex + 1];
            var targetFolder = folderIndex >= 0 && folderIndex + 1 < cliArgs.Length 
                ? cliArgs[folderIndex + 1] 
                : "INBOX";
            
            if (!int.TryParse(accountIdStr, out var targetAccountId))
            {
                Console.WriteLine($"ERROR: Invalid account-id: {accountIdStr}");
                Environment.Exit(1);
            }
            
            // Validate file exists
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"ERROR: File not found: {filePath}");
                Environment.Exit(1);
            }
            
            // Validate path is in allowed paths
            var localImportOptions = cliServices.GetRequiredService<IOptions<LocalImportOptions>>().Value;
            var normalizedPath = Path.GetFullPath(filePath);
            var isAllowed = localImportOptions.AllowedPaths.Any(allowed =>
            {
                var normalizedAllowed = Path.GetFullPath(allowed);
                return normalizedPath.StartsWith(normalizedAllowed, StringComparison.OrdinalIgnoreCase);
            });
            
            if (!isAllowed)
            {
                Console.WriteLine($"ERROR: File path '{filePath}' is not in an allowed import directory.");
                Console.WriteLine("Allowed paths (configured in appsettings.json -> LocalImport -> AllowedPaths):");
                foreach (var allowed in localImportOptions.AllowedPaths)
                    Console.WriteLine($"  - {Path.GetFullPath(allowed)}");
                Console.WriteLine("Add the directory to 'LocalImport.AllowedPaths' in appsettings.json, or mount your files into an allowed directory.");
                Environment.Exit(1);
            }
            
            // Verify target account exists
            using (var checkScope = cliServices.CreateScope())
            {
                var checkContext = checkScope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();
                var account = await checkContext.MailAccounts.FindAsync(targetAccountId);
                if (account == null)
                {
                    Console.WriteLine($"ERROR: Mail account with ID {targetAccountId} not found in database.");
                    Environment.Exit(1);
                }
                Console.WriteLine($"Target account: {account.EmailAddress} (ID: {account.Id})");
            }
            
            var fileInfo = new FileInfo(filePath);
            Console.WriteLine($"\n=== Local {formatLabel} Import ===");
            Console.WriteLine($"File: {filePath}");
            Console.WriteLine($"Size: {fileInfo.Length / (1024.0 * 1024.0):F2} MB");
            Console.WriteLine($"Target Account ID: {targetAccountId}");
            Console.WriteLine($"Target Folder: {targetFolder}");
            Console.WriteLine();
            
            var startTime = DateTime.UtcNow;
            
            if (isMbox)
            {
                var mboxService = cliServices.GetRequiredService<IMBoxImportService>();
                var result = await mboxService.ProcessFileAsync(filePath, Path.GetFileName(filePath), targetAccountId, targetFolder, "CLI");
                
                Console.WriteLine($"\n=== Import Results ===");
                Console.WriteLine($"Status: {result.Status}");
                Console.WriteLine($"Total Emails: {result.TotalEmails}");
                Console.WriteLine($"Imported Successfully: {result.SuccessCount}");
                Console.WriteLine($"Failed: {result.FailedCount}");
                Console.WriteLine($"Skipped (malformed): {result.SkippedMalformedCount}");
                Console.WriteLine($"Skipped (duplicates): {result.SkippedAlreadyExistsCount}");
                Console.WriteLine($"Duration: {DateTime.UtcNow - startTime}");
                if (!string.IsNullOrEmpty(result.ErrorMessage))
                    Console.WriteLine($"Errors: {result.ErrorMessage}");
                
                Environment.Exit(MBoxImportExitCode.For(result.Status, result.FailedCount, result.SkippedMalformedCount));
            }
            else
            {
                var emlService = cliServices.GetRequiredService<IEmlImportService>();
                var result = await emlService.ProcessFileAsync(filePath, Path.GetFileName(filePath), targetAccountId, "CLI");
                
                Console.WriteLine($"\n=== Import Results ===");
                Console.WriteLine($"Status: {result.Status}");
                Console.WriteLine($"Total Emails: {result.TotalEmails}");
                Console.WriteLine($"Imported Successfully: {result.SuccessCount}");
                Console.WriteLine($"Failed: {result.FailedCount}");
                Console.WriteLine($"Skipped (duplicates): {result.SkippedAlreadyExistsCount}");
                Console.WriteLine($"Duration: {DateTime.UtcNow - startTime}");
                if (!string.IsNullOrEmpty(result.ErrorMessage))
                    Console.WriteLine($"Errors: {result.ErrorMessage}");
                
                Environment.Exit(result.Status == EmlImportJobStatus.Completed ? 0 : 1);
            }
        }
    }
    catch (Exception ex)
    {
        cliLogger.LogError(ex, "CLI command failed");
        Console.WriteLine($"ERROR: {ex.Message}");
        Environment.Exit(1);
    }
}

// Handle CLI command: date-windowed offload into another mailbox
if (cliArgs.Any(a => a == "--offload"))
{
    using var offloadScope = app.Services.CreateScope();
    var offloadServices = offloadScope.ServiceProvider;
    var offloadLogger = offloadServices.GetRequiredService<ILogger<Program>>();

    // Exit codes: 0 everything appended or already present, 1 a failure occurred,
    // 2 the invocation itself was wrong. Distinguishing 2 from 1 matters when driving many
    // mailboxes from a script.
    const int ExitOk = 0, ExitFailed = 1, ExitBadArgs = 2;

    string? Arg(string name)
    {
        var i = Array.IndexOf(cliArgs, name);
        return i >= 0 && i + 1 < cliArgs.Length ? cliArgs[i + 1] : null;
    }
    bool Flag(string name) => cliArgs.Contains(name);

    try
    {
        var sourceRaw = Arg("--source-account-id");
        var targetRaw = Arg("--target-account-id");
        var sinceRaw = Arg("--since");
        var untilRaw = Arg("--until");
        var targetFolder = Arg("--target-folder") ?? "INBOX";
        var preserveFolders = Flag("--preserve-folders");
        var dryRun = Flag("--dry-run");
        var noMarkSeen = Flag("--no-mark-seen");

        void Usage(string message)
        {
            Console.WriteLine($"ERROR: {message}");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  --offload --source-account-id N --target-account-id M --since YYYY-MM-DD");
            Console.WriteLine("            [--until YYYY-MM-DD] [--target-folder INBOX]");
            Console.WriteLine("            [--preserve-folders] [--dry-run] [--no-mark-seen]");
            Console.WriteLine();
            Console.WriteLine("Appends archived mail newer than --since into the target mailbox, skipping");
            Console.WriteLine("anything already present there. Repeating a run is safe.");
            Console.WriteLine();
            Console.WriteLine("Folder exclusions and the folder rename map come from the Offload section of");
            Console.WriteLine("appsettings.json; both are empty by default.");
        }

        if (!int.TryParse(sourceRaw, out var sourceAccountId))
        {
            Usage("--source-account-id <id> is required");
            Environment.Exit(ExitBadArgs);
        }
        if (!int.TryParse(targetRaw, out var targetAccountId))
        {
            Usage("--target-account-id <id> is required");
            Environment.Exit(ExitBadArgs);
        }
        if (sourceAccountId == targetAccountId)
        {
            Usage("source and target account must differ");
            Environment.Exit(ExitBadArgs);
        }
        if (!DateTime.TryParse(sinceRaw, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var since))
        {
            Usage("--since <YYYY-MM-DD> is required");
            Environment.Exit(ExitBadArgs);
        }

        DateTime? until = null;
        if (!string.IsNullOrEmpty(untilRaw))
        {
            if (!DateTime.TryParse(untilRaw, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var untilParsed))
            {
                Usage("--until must be YYYY-MM-DD");
                Environment.Exit(ExitBadArgs);
            }
            until = untilParsed;
            if (until.Value.Date < since.Date)
            {
                Usage("--until must not be earlier than --since");
                Environment.Exit(ExitBadArgs);
            }
        }

        var offloadDb = offloadServices.GetRequiredService<MailArchiverDbContext>();
        var sourceAccount = await offloadDb.MailAccounts.FindAsync(sourceAccountId);
        var targetAccount = await offloadDb.MailAccounts.FindAsync(targetAccountId);

        if (sourceAccount == null)
        {
            Console.WriteLine($"ERROR: source account {sourceAccountId} not found");
            Environment.Exit(ExitBadArgs);
        }
        if (targetAccount == null)
        {
            Console.WriteLine($"ERROR: target account {targetAccountId} not found");
            Environment.Exit(ExitBadArgs);
        }
        if (targetAccount!.Provider != ProviderType.IMAP)
        {
            Console.WriteLine($"ERROR: target account '{targetAccount.Name}' is a {targetAccount.Provider} account; " +
                              "offload targets must be IMAP");
            Environment.Exit(ExitBadArgs);
        }
        // Same rule the queued job path and the UI apply, so all three agree.
        if (!targetAccount.IsEnabled)
        {
            Console.WriteLine($"ERROR: target account '{targetAccount.Name}' is disabled");
            Environment.Exit(ExitBadArgs);
        }

        var offloadOptions = offloadServices.GetRequiredService<IOptions<OffloadOptions>>().Value;
        var batchRestoreOptions = offloadServices.GetRequiredService<IOptions<BatchRestoreOptions>>().Value;

        var criteria = new OffloadCriteria
        {
            SourceAccountId = sourceAccountId,
            // Resolved to an absolute date here, once, so that a repeated run selects the same mail.
            CutoffFrom = MailArchiver.Services.Shared.OffloadCutoff.FromAbsolute(since),
            CutoffTo = until,
            ExcludedSourceFolders = offloadOptions.ExcludedSourceFolders,
            FolderRenameMap = offloadOptions.FolderRenameMap,
            MarkAsSeen = !noMarkSeen && offloadOptions.MarkAsSeen,
            DryRun = dryRun,
        };

        var upperBound = until.HasValue ? until.Value.Date.AddDays(1).AddSeconds(-1) : (DateTime?)null;
        var idsQuery = offloadDb.ArchivedEmails
            .Where(e => e.MailAccountId == sourceAccountId && e.SentDate >= criteria.CutoffFrom);
        if (upperBound.HasValue)
            idsQuery = idsQuery.Where(e => e.SentDate <= upperBound.Value);

        var emailIds = await idsQuery.OrderBy(e => e.Id).Select(e => e.Id).ToListAsync();

        Console.WriteLine();
        Console.WriteLine("=== Offload ===");
        Console.WriteLine($"Source:         {sourceAccount!.Name} (ID {sourceAccountId})");
        Console.WriteLine($"Target:         {targetAccount.Name} (ID {targetAccountId})");
        Console.WriteLine($"Window:         {criteria.DescribeWindow()}");
        Console.WriteLine($"Target folder:  {targetFolder}");
        Console.WriteLine($"Preserve tree:  {preserveFolders}");
        Console.WriteLine($"Mark as seen:   {criteria.MarkAsSeen}");
        Console.WriteLine($"Excluded:       {(criteria.ExcludedSourceFolders.Count == 0 ? "(none)" : string.Join(", ", criteria.ExcludedSourceFolders))}");
        Console.WriteLine($"Rename map:     {(criteria.FolderRenameMap.Count == 0 ? "(empty)" : string.Join(", ", criteria.FolderRenameMap.Select(kv => $"{kv.Key} -> {kv.Value}")))}");
        Console.WriteLine($"Resolved mails: {emailIds.Count}");
        Console.WriteLine($"Dry run:        {dryRun}");
        Console.WriteLine();

        if (emailIds.Count == 0)
        {
            Console.WriteLine("Nothing to do: no archived mail in that window.");
            Environment.Exit(ExitOk);
        }

        // Kept as a sanity guard rather than a binding constraint: a date window brings a very
        // large mailbox well under this, but an accidentally huge window should still be caught.
        if (emailIds.Count > batchRestoreOptions.MaxAsyncEmails)
        {
            Console.WriteLine($"ERROR: {emailIds.Count} mails exceeds BatchRestore.MaxAsyncEmails " +
                              $"({batchRestoreOptions.MaxAsyncEmails}). Narrow the window or raise the limit.");
            Environment.Exit(ExitBadArgs);
        }

        var imapService = offloadServices.GetRequiredService<MailArchiver.Services.Providers.ImapEmailService>();

        var lastReported = 0;
        var outcome = await imapService.OffloadEmailsAsync(
            emailIds, targetAccountId, targetFolder, preserveFolders, criteria,
            (processed, appended, failed) =>
            {
                // One line per hundred keeps a multi-hour run readable in a log file.
                if (processed - lastReported < 100 && processed != emailIds.Count) return;
                lastReported = processed;
                Console.WriteLine($"  {processed}/{emailIds.Count} processed, {appended} appended, {failed} failed");
            },
            CancellationToken.None);

        Console.WriteLine();
        Console.Write(outcome.Describe());

        try
        {
            var accessLog = offloadServices.GetRequiredService<IAccessLogService>();
            await accessLog.LogAccessAsync("CLI", AccessLogType.Restore,
                mailAccountId: sourceAccountId,
                searchParameters: $"offload source={sourceAccountId} target={targetAccountId} " +
                                  $"window={criteria.DescribeWindow()} folder={targetFolder} " +
                                  $"preserve={preserveFolders} dryRun={dryRun} " +
                                  $"appended={outcome.Appended} alreadyPresent={outcome.SkippedAlreadyPresent} " +
                                  $"failed={outcome.Failed}");
        }
        catch (Exception ex)
        {
            offloadLogger.LogWarning(ex, "Could not write the offload audit entry");
        }

        Environment.Exit(outcome.Failed == 0 ? ExitOk : ExitFailed);
    }
    catch (Exception ex)
    {
        offloadLogger.LogError(ex, "Offload failed");
        Console.WriteLine($"ERROR: {ex.Message}");
        Environment.Exit(ExitFailed);
    }
}

// Datenbank initialisieren
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<MailArchiverDbContext>();
        try
        {
            // Ensure __EFMigrationsHistory table exists before running migrations
            await EnsureMigrationsHistoryTableExists(context, services);
            
            // Now run migrations
            context.Database.Migrate();
        }
        catch (Exception ex)
        {
            // If migrations fail, it might be a completely new database
            // In this case, ensure the database exists and then try migrations again
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogWarning(ex, "Migration failed, attempting to create database structure");
            
            // Ensure database exists
            context.Database.EnsureCreated();
            
            // Ensure __EFMigrationsHistory table exists before running migrations again
            await EnsureMigrationsHistoryTableExists(context, services);
            
            // Try migrations again
            context.Database.Migrate();
        }
        context.Database.ExecuteSqlRaw("CREATE EXTENSION IF NOT EXISTS citext;");

        // Create admin user if it doesn't exist
        var authOptions = services.GetRequiredService<IOptions<AuthenticationOptions>>().Value;
        if (authOptions.Enabled)
        {
            var userService = services.GetRequiredService<IUserService>();
            var adminUser = await userService.GetUserByUsernameAsync(authOptions.Username);
            if (adminUser == null)
            {
                var adminEmail = $"{authOptions.Username}@local";
                adminUser = await userService.CreateUserAsync(
                    authOptions.Username,
                    adminEmail,
                    authOptions.Password,
                    true);
                var userLogger = services.GetRequiredService<ILogger<Program>>();
                userLogger.LogInformation("Admin user created: {Username} with email {Email}", authOptions.Username, adminEmail);
            }
        }

        var initLogger = services.GetRequiredService<ILogger<Program>>();
        initLogger.LogInformation("Datenbank wurde initialisiert");

        // Apply deletion policy: lock/unlock all archived emails based on configuration
        var deletionPolicy = services.GetRequiredService<IOptions<DeletionPolicyOptions>>().Value;
        await ApplyDeletionPolicyAsync(context, deletionPolicy, initLogger);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Ein Fehler ist bei der Datenbankinitialisierung aufgetreten");
    }
}

/// <summary>
/// Applies the deletion policy on startup: adjusts the column default so that
/// newly imported emails follow the configured policy, and logs the resulting
/// state to the AccessLogs table (visible on the Logs page) for auditability.
/// The potentially expensive row-by-row UPDATE of existing archived emails is
/// performed asynchronously by DeletionPolicyApplicationService so that the
/// startup is not blocked on large archives.
/// </summary>
static async Task ApplyDeletionPolicyAsync(MailArchiverDbContext context, DeletionPolicyOptions policy, ILogger<Program> logger)
{
    var deletionAllowed = policy.DeletionAllowed;
    var lockValue = !deletionAllowed;
    // Inline boolean literal (Npgsql does not support parameters in DDL statements)
    var lockLiteral = lockValue ? "TRUE" : "FALSE";

    try
    {
        // Adjust the column default so that newly imported emails follow the same policy.
        // DDL statements cannot use Npgsql parameters, so the boolean is inlined.
        await context.Database.ExecuteSqlRawAsync(
            $@"ALTER TABLE mail_archiver.""ArchivedEmails"" ALTER COLUMN ""IsLocked"" SET DEFAULT {lockLiteral};");

        logger.LogInformation("Deletion policy default applied: DeletionAllowed={DeletionAllowed}, column default IsLocked={IsLocked}. Existing rows will be updated by the background service.",
            deletionAllowed, lockValue);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to apply deletion policy column default");
    }

    // Log the policy state to the AccessLogs table for auditability on the Logs page
    try
    {
        var logEntry = new AccessLog
        {
            Username = "SYSTEM",
            Type = AccessLogType.DeletionPolicy,
            Timestamp = DateTime.UtcNow,
            SearchParameters = deletionAllowed
                ? "Email deletion is enabled by configuration (DeletionPolicy:DeletionAllowed=true). Archived emails are unlocked."
                : "Email deletion is disabled by configuration (DeletionPolicy:DeletionAllowed=false). Archived emails are locked (compliance)."
        };

        context.AccessLogs.Add(logEntry);
        await context.SaveChangesAsync();
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Failed to log deletion policy state to AccessLogs");
    }
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// API errors must be problem+json, never the /Home/Error HTML page (and never a
// login redirect). This /api-scoped handler is registered AFTER the global one
// so it sits *inside* it: for /api paths it catches exceptions first and emits
// JSON; non-/api paths skip this branch and fall through to /Home/Error.
app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/api"),
    apiBranch => apiBranch.UseExceptionHandler(new ExceptionHandlerOptions
    {
        ExceptionHandler = static async context =>
        {
            var problemDetailsService = context.RequestServices
                .GetRequiredService<Microsoft.AspNetCore.Http.IProblemDetailsService>();
            context.Response.ContentType = "application/problem+json";
            await problemDetailsService.WriteAsync(new Microsoft.AspNetCore.Http.ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails =
                {
                    Status = context.Response.StatusCode,
                    Title = "An unexpected error occurred.",
                    Instance = context.Request.Path
                }
            });
        }
    }));

// MCP endpoint errors must also be JSON, never the /Home/Error HTML page.
// Same pattern as the /api handler above.
app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/mcp"),
    mcpBranch => mcpBranch.UseExceptionHandler(new ExceptionHandlerOptions
    {
        ExceptionHandler = static async context =>
        {
            var problemDetailsService = context.RequestServices
                .GetRequiredService<Microsoft.AspNetCore.Http.IProblemDetailsService>();
            context.Response.ContentType = "application/problem+json";
            await problemDetailsService.WriteAsync(new Microsoft.AspNetCore.Http.ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails =
                {
                    Status = context.Response.StatusCode,
                    Title = "An unexpected error occurred.",
                    Instance = context.Request.Path
                }
            });
        }
    }));

// Use Forwarded Headers middleware for reverse proxy support
app.UseForwardedHeaders();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture("en")
    .AddSupportedCultures("en", "en-GB", "de", "es", "fr", "it", "sl", "nl", "ru", "hu", "pl")
    .AddSupportedUICultures("en", "en-GB", "de", "es", "fr", "it", "sl", "nl", "ru", "hu", "pl"));
app.UseRouting();
app.UseSession();

// Add Rate Limiting Middleware
app.UseRateLimiter();

// Add our custom authentication middleware
app.UseAuth();

// OpenAPI document + Swagger UI for the read-only REST API. Mapped only when the
// API and its UI are enabled. Both paths sit OUTSIDE /api/, so the cookie
// middleware above gates them — a logged-in browser session is required, and
// they are unreachable with an API key.
var apiUiOptions = app.Services.GetRequiredService<IOptions<MailArchiver.Models.ApiOptions>>().Value;
if (apiUiOptions.Enabled && apiUiOptions.EnableSwaggerUi)
{
    app.MapOpenApi("/apidocs/spec/{documentName}.json");
    app.UseSwaggerUI(swaggerOptions =>
    {
        swaggerOptions.SwaggerEndpoint("/apidocs/spec/v1.json", "Mail Archiver API v1");
        swaggerOptions.RoutePrefix = "apidocs";
    });
}

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// MCP (Model Context Protocol) Streamable HTTP endpoint. Mapped only when the
// MCP server is enabled, so disabled deployments expose nothing at /mcp.
// The "Mcp" rate-limit policy (registered above) is enforced on the endpoint
// directly via RequireRateLimiting, because MCP tools are not MVC controllers
// and therefore cannot receive [EnableRateLimiting] attributes — without this
// the /mcp endpoint would be effectively unthrottled and exposed to
// brute-force / DoS when published to the internet.
var mcpOptions = app.Services.GetRequiredService<IOptions<MailArchiver.Models.McpOptions>>().Value;
if (mcpOptions.Enabled)
{
    app.MapMcp("/mcp").RequireRateLimiting("Mcp");
}

app.Run();

/// <summary>
/// Decides the process exit code for an <c>--import-mbox</c> run.
/// <para>
/// A repeated import used to exit non-zero although nothing had gone wrong. The job status
/// alone cannot carry that distinction: <see cref="MBoxImportJobStatus.CompletedWithErrors"/>
/// is set when anything at all was not imported cleanly, and skipped duplicates count towards
/// that. Re-importing the same mbox therefore looked like a failure to any script driving the
/// CLI, even though skipping what is already archived is the correct and expected outcome.
/// </para>
/// <para>
/// So the status is no longer the whole answer, but it cannot be dropped either. Deriving the
/// exit code purely from the counters would report success for a cancelled or crashed import,
/// because those paths abandon the run without setting <c>FailedCount</c> or
/// <c>SkippedMalformedCount</c>. Both halves are needed: the run has to have reached a
/// completed status, <em>and</em> nothing may have failed or been malformed.
/// </para>
/// <para>
/// Duplicates are deliberately absent from the failure conditions — they are the case this
/// fixes. The status the UI shows is unchanged; only the exit code is derived differently.
/// </para>
/// </summary>
internal static class MBoxImportExitCode
{
    public static int For(MBoxImportJobStatus status, int failedCount, int skippedMalformedCount)
    {
        var reachedCompletion = status == MBoxImportJobStatus.Completed
                             || status == MBoxImportJobStatus.CompletedWithErrors;

        return reachedCompletion && failedCount == 0 && skippedMalformedCount == 0 ? 0 : 1;
    }
}

using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Middleware;
using MailArchiver.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using System.Globalization;
using System.Data.Common;
using System.Threading.RateLimiting;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

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

// Add Authentication Options
builder.Services.Configure<AuthenticationOptions>(
    builder.Configuration.GetSection(AuthenticationOptions.Authentication));

// Add Batch Restore Options
builder.Services.Configure<BatchRestoreOptions>(
    builder.Configuration.GetSection(BatchRestoreOptions.BatchRestore));

// Add Batch Operation Options
builder.Services.Configure<BatchOperationOptions>(
    builder.Configuration.GetSection(BatchOperationOptions.BatchOperation));

// Add Mail Sync Options
builder.Services.Configure<MailSyncOptions>(
    builder.Configuration.GetSection(MailSyncOptions.MailSync));

// Add Upload Options
builder.Services.Configure<UploadOptions>(
    builder.Configuration.GetSection(UploadOptions.Upload));

// Add Session support
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

// Add Data Protection with persistent key storage
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("/app/DataProtection-Keys"))
    .SetApplicationName("MailArchiver");

// Add Rate Limiting
builder.Services.AddRateLimiter(options =>
{
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
    
    // Rejection response
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = 429;
        
        if (context.Lease.TryGetMetadata("RETRY_AFTER", out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = ((TimeSpan)retryAfter).TotalSeconds.ToString();
        }
        
        // Get localizer for rate limit message
        var serviceProvider = context.HttpContext.RequestServices;
        var localizer = serviceProvider.GetService<Microsoft.Extensions.Localization.IStringLocalizer<MailArchiver.SharedResource>>();
        var message = localizer?["RateLimitExceeded"] ?? "Rate limit exceeded. Please try again later.";
        
        await context.HttpContext.Response.WriteAsync(message, cancellationToken: token);
    };
});

// Add Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/AccessDenied";
        options.Cookie.Name = "MailArchiverAuth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
        options.SlidingExpiration = true;
    });

// Set global encoding to UTF-8
System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

// PostgreSQL-Datenbankkontext hinzufügen
builder.Services.AddDbContext<MailArchiverDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    
    options.UseNpgsql(
        connectionString,
        npgsqlOptions => {
            npgsqlOptions.CommandTimeout(
                builder.Configuration.GetValue<int>("Npgsql:CommandTimeout", 600) // 10 Minuten Standardwert
            );
        }
    )
    .ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    
    // Enable sensitive data logging for debugging (remove in production)
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
    }
});

// Services hinzufügen
builder.Services.AddScoped<IEmailService, EmailService>(provider =>
    new EmailService(
        provider.GetRequiredService<MailArchiverDbContext>(),
        provider.GetRequiredService<ILogger<EmailService>>(),
        provider.GetRequiredService<ISyncJobService>(),
        provider.GetRequiredService<IOptions<BatchOperationOptions>>(),
        provider.GetRequiredService<IOptions<MailSyncOptions>>(),
        provider.GetRequiredService<IGraphEmailService>()
    ));
builder.Services.AddScoped<IGraphEmailService, GraphEmailService>();
builder.Services.AddScoped<IAuthenticationService, CookieAuthenticationService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddSingleton<ISyncJobService, SyncJobService>(); // NEUE SERVICE

// Register BatchRestoreService as singleton and hosted service - MUST be the same instance
builder.Services.AddSingleton<BatchRestoreService>();
builder.Services.AddSingleton<IBatchRestoreService>(provider => provider.GetRequiredService<BatchRestoreService>());
builder.Services.AddHostedService<BatchRestoreService>(provider => provider.GetRequiredService<BatchRestoreService>());

// Register MBoxImportService as singleton and hosted service - MUST be the same instance
builder.Services.AddSingleton<MBoxImportService>();
builder.Services.AddSingleton<IMBoxImportService>(provider => provider.GetRequiredService<MBoxImportService>());
builder.Services.AddHostedService<MBoxImportService>(provider => provider.GetRequiredService<MBoxImportService>());

// Register EmlImportService as singleton and hosted service - MUST be the same instance
builder.Services.AddSingleton<EmlImportService>();
builder.Services.AddSingleton<IEmlImportService>(provider => provider.GetRequiredService<EmlImportService>());
builder.Services.AddHostedService<EmlImportService>(provider => provider.GetRequiredService<EmlImportService>());
builder.Services.AddHostedService<MailSyncBackgroundService>();

// Add Localization
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
// Configure Form Options for large file uploads
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    var uploadOptions = builder.Configuration.GetSection(UploadOptions.Upload).Get<UploadOptions>() ?? new UploadOptions();
    
    options.MultipartBodyLengthLimit = uploadOptions.MaxFileSizeBytes;
    options.ValueLengthLimit = (int)Math.Min(uploadOptions.MaxFileSizeBytes, int.MaxValue);
    options.MultipartHeadersLengthLimit = (int)Math.Min(uploadOptions.MaxFileSizeBytes, int.MaxValue);
    options.MemoryBufferThreshold = int.MaxValue;
    options.BufferBody = false; // Stream large files directly to disk
});

// MVC hinzufügen
builder.Services.AddControllersWithViews()
    .AddViewLocalization();

builder.Services.Configure<AuthenticationOptions>(
    builder.Configuration.GetSection(AuthenticationOptions.Authentication));

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
                adminUser = await userService.CreateUserAsync(
                    authOptions.Username,
                    "admin@local",
                    authOptions.Password,
                    true);
                var userLogger = services.GetRequiredService<ILogger<Program>>();
                userLogger.LogInformation("Admin user created: {Username}", authOptions.Username);
            }
        }

        var initLogger = services.GetRequiredService<ILogger<Program>>();
        initLogger.LogInformation("Datenbank wurde initialisiert");
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Ein Fehler ist bei der Datenbankinitialisierung aufgetreten");
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

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture("en")
    .AddSupportedCultures("en", "de", "es", "fr", "it", "sl", "nl", "ru")
    .AddSupportedUICultures("en", "de", "es", "fr", "it", "sl", "nl", "ru"));
app.UseRouting();
app.UseSession();

// Add Rate Limiting Middleware
app.UseRateLimiter();

// Add our custom authentication middleware
app.UseMiddleware<AuthenticationMiddleware>();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

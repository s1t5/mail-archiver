using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Middleware;
using MailArchiver.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Data.Common;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// Helper method to ensure __EFMigrationsHistory table exists
async static Task EnsureMigrationsHistoryTableExists(MailArchiverDbContext context, IServiceProvider services)
{
    var connection = context.Database.GetDbConnection();
    await connection.OpenAsync();
    
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

// Add Session support
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

// Add Authentication
builder.Services.AddAuthentication("MailArchiverAuth")
    .AddCookie("MailArchiverAuth", options =>
    {
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/AccessDenied";
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
    );
    
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
        provider.GetRequiredService<IOptions<BatchOperationOptions>>()
    ));
builder.Services.AddScoped<IAuthenticationService>(provider =>
    new SimpleAuthenticationService(
        provider.GetRequiredService<IOptions<AuthenticationOptions>>(),
        provider.GetRequiredService<IUserService>(),
        provider.GetRequiredService<MailArchiverDbContext>(),
        provider.GetRequiredService<ILogger<SimpleAuthenticationService>>()
    ));
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddSingleton<ISyncJobService, SyncJobService>(); // NEUE SERVICE
builder.Services.AddSingleton<IBatchRestoreService, BatchRestoreService>();
builder.Services.AddSingleton<IMBoxImportService, MBoxImportService>();

builder.Services.AddHostedService<BatchRestoreService>(provider =>
    new BatchRestoreService(
        provider.GetRequiredService<IServiceProvider>(),
        provider.GetRequiredService<ILogger<BatchRestoreService>>(),
        provider.GetRequiredService<IOptions<BatchOperationOptions>>()
    ));
builder.Services.AddHostedService<MBoxImportService>(provider =>
    new MBoxImportService(
        provider.GetRequiredService<IServiceProvider>(),
        provider.GetRequiredService<ILogger<MBoxImportService>>(),
        provider.GetRequiredService<IWebHostEnvironment>(),
        provider.GetRequiredService<IOptions<BatchOperationOptions>>()
    ));
builder.Services.AddHostedService<MailSyncBackgroundService>();

// MVC hinzufügen
builder.Services.AddControllersWithViews();

builder.Services.Configure<AuthenticationOptions>(
    builder.Configuration.GetSection(AuthenticationOptions.Authentication));

builder.Services.Configure<BatchRestoreOptions>(
    builder.Configuration.GetSection(BatchRestoreOptions.BatchRestore));


// Kestrel-Server-Limits konfigurieren
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = int.MaxValue;
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(30);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(20);
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
app.UseRouting();
app.UseSession();

// Add our custom authentication middleware
app.UseMiddleware<AuthenticationMiddleware>();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

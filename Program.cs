using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Middleware;
using MailArchiver.Services;
using Microsoft.EntityFrameworkCore;

// Add this line to enable legacy timestamp behavior
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Add Authentication Options
builder.Services.Configure<AuthenticationOptions>(
    builder.Configuration.GetSection(AuthenticationOptions.Authentication));

// Add Batch Restore Options
builder.Services.Configure<BatchRestoreOptions>(
    builder.Configuration.GetSection(BatchRestoreOptions.BatchRestore));

// Add Session support
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

// PostgreSQL-Datenbankkontext hinzufügen
builder.Services.AddDbContext<MailArchiverDbContext>(options =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsqlOptions => npgsqlOptions.CommandTimeout(
            builder.Configuration.GetValue<int>("Npgsql:CommandTimeout", 600) // 10 Minuten Standardwert
        )
    );
});

// Services hinzufügen
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IAuthenticationService, SimpleAuthenticationService>();
builder.Services.AddSingleton<ISyncJobService, SyncJobService>(); // NEUE SERVICE
builder.Services.AddSingleton<IBatchRestoreService, BatchRestoreService>();
builder.Services.AddHostedService<BatchRestoreService>(provider =>
    (BatchRestoreService)provider.GetRequiredService<IBatchRestoreService>());
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
        context.Database.EnsureCreated();
        context.Database.ExecuteSqlRaw("CREATE EXTENSION IF NOT EXISTS citext;");
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Datenbank wurde initialisiert");
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
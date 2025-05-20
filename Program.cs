using MailArchiver.Data;
using MailArchiver.Services;
using Microsoft.EntityFrameworkCore;

// Add this line to enable legacy timestamp behavior
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddHostedService<MailSyncBackgroundService>();

// MVC hinzufügen
builder.Services.AddControllersWithViews();

// Kestrel-Server-Limits konfigurieren
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = int.MaxValue; // Maximale Request-Größe (oder einen angemessenen Wert)
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

        // Datenbank und Schema erstellen, wenn sie nicht existieren
        context.Database.EnsureCreated();

        // PostgreSQL citext-Erweiterung aktivieren (falls noch nicht vorhanden)
        context.Database.ExecuteSqlRaw("CREATE EXTENSION IF NOT EXISTS citext;");

        // Log-Nachricht
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

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
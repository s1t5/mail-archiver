using MailArchiver.Auth.Options;
using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services;
using MailArchiver.Services.Core;
using MailArchiver.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MailArchiver.Tests.Infrastructure;

/// <summary>
/// Factory for constructing service instances against a given <see cref="MailArchiverDbContext"/>
/// with minimal, test-friendly dependencies (NullLogger, default options, stub localizer/config).
/// </summary>
internal static class ServiceFactory
{
    public static EmailCoreService CreateEmailCoreService(MailArchiverDbContext ctx) =>
        new(ctx,
            NullLogger<EmailCoreService>.Instance,
            new DateTimeHelper(Options.Create(new TimeZoneOptions { DisplayTimeZoneId = "Europe/Berlin" })),
            Options.Create(new BatchOperationOptions()));

    public static BandwidthService CreateBandwidthService(MailArchiverDbContext ctx, BandwidthTrackingOptions? options = null) =>
        new(ctx,
            NullLogger<BandwidthService>.Instance,
            Options.Create(options ?? new BandwidthTrackingOptions { Enabled = true, DailyLimitMb = 10 }));

    public static UserService CreateUserService(MailArchiverDbContext ctx, OAuthOptions? oauth = null) =>
        new(ctx,
            NullLogger<UserService>.Instance,
            new NullStringLocalizer<SharedResource>(),
            Options.Create(oauth ?? new OAuthOptions()));

    public static AccountStorageService CreateAccountStorageService(MailArchiverDbContext ctx) =>
        new(ctx,
            NullLogger<AccountStorageService>.Instance,
            new ConfigurationBuilder().Build());

    /// <summary>
    /// Builds a <see cref="ServiceProvider"/> that resolves the shared
    /// <see cref="MailArchiverDbContext"/> from every scope, so services that call
    /// <c>CreateScope().GetRequiredService&lt;MailArchiverDbContext&gt;()</c> receive a context
    /// sharing the same connection/transaction as the test. Registered as Singleton so that
    /// disposing the scope does NOT dispose the shared context (and its transaction).
    /// </summary>
    public static IServiceProvider BuildScopedProviderFor(MailArchiverDbContext sharedContext)
    {
        var services = new ServiceCollection();
        services.AddSingleton(sharedContext);
        return services.BuildServiceProvider();
    }
}

/// <summary>
/// Minimal <see cref="IStringLocalizer{T}"/> that returns the key as the value,
/// so resource lookups in <see cref="UserService"/> never throw.
/// </summary>
internal sealed class NullStringLocalizer<T> : IStringLocalizer<T>
{
    public LocalizedString this[string name] => new(name, name, false);
    public LocalizedString this[string name, params object[] arguments] =>
        new(name, string.Format(name, arguments), false);
    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
        Enumerable.Empty<LocalizedString>();
}
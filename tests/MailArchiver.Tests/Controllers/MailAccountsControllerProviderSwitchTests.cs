using MailArchiver.Controllers;
using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services;
using MailArchiver.Services.Factories;
using MailArchiver.Services.Mailcow;
using MailArchiver.Services.Providers;
using MailArchiver.Services.Security;
using MailArchiver.Services.Shared;
using MailArchiver.Utilities;
using MailArchiver.Models.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace MailArchiver.Tests.Controllers;

/// <summary>
/// Regression tests for the Edit POST of <see cref="MailAccountsController"/>:
/// switching an account to the MSA provider must not keep a client secret that belongs
/// to the previous provider (e.g. an M365 app registration) or that cannot be used with
/// the shared public default client id. Leftover secrets were sent as client_secret on
/// every MSA token refresh (see issue #592).
///
/// These tests use SQLite in-memory instead of the shared PostgreSQL fixture: the
/// controller only needs a working relational store for this code path, and the
/// behaviour must be verifiable without a database server.
/// </summary>
public class MailAccountsControllerProviderSwitchTests
{
    private const string DefaultClientId = "d0c86bbc-b89f-4539-a024-93a425ade844";

    private sealed class SqliteDb : IDisposable
    {
        private readonly SqliteConnection _connection;
        public MailArchiverDbContext Context { get; }

        public SqliteDb()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            var options = new DbContextOptionsBuilder<MailArchiverDbContext>()
                .UseSqlite(_connection)
                .ConfigureWarnings(w => w.Ignore(
                    Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
                .Options;
            Context = new MailArchiverDbContext(options);
            Context.Database.EnsureCreated();
        }

        public void Dispose()
        {
            Context.Dispose();
            _connection.Dispose();
        }
    }

    private sealed class PassThroughLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();
    }

    private static MailAccountsController CreateController(MailArchiverDbContext context, bool hasDefaultClientId)
    {
        var msaOptions = new MsaOAuthOptions
        {
            DefaultClientId = hasDefaultClientId ? DefaultClientId : null,
            Authority = "https://login.microsoftonline.com/common/oauth2/v2.0",
        };

        var dateTimeHelper = new DateTimeHelper(Options.Create(new TimeZoneOptions()));
        var services = new ServiceCollectionShim();
        var httpContext = new DefaultHttpContext { RequestServices = services };

        var controller = new MailAccountsController(
            context,
            new MailArchiver.Services.Core.EmailCoreService(
                context,
                NullLogger<MailArchiver.Services.Core.EmailCoreService>.Instance,
                dateTimeHelper,
                Options.Create(new BatchOperationOptions())),
            new ProviderEmailServiceFactory(services, context, NullLogger<ProviderEmailServiceFactory>.Instance),
            Mock.Of<IGraphEmailService>(),
            NullLogger<MailAccountsController>.Instance,
            Options.Create(new BatchRestoreOptions()),
            Options.Create(new TenantManagementOptions()),
            Mock.Of<ISyncJobService>(),
            Mock.Of<IMBoxImportService>(),
            Mock.Of<IEmlImportService>(),
            Options.Create(new UploadOptions()),
            new PassThroughLocalizer(),
            services,
            Mock.Of<IExportService>(),
            Mock.Of<IAccessLogService>(),
            Mock.Of<IMailAccountDeletionService>(),
            Mock.Of<IMsaOAuthService>(),
            Options.Create(msaOptions),
            Mock.Of<IAccountStorageService>(),
            Options.Create(new CsvImportOptions()),
            Options.Create(new OffloadOptions()),
            Options.Create(new MailSyncOptions()),
            Mock.Of<IBatchRestoreService>(),
            new MailcowProvisioningService(
                context,
                new MailcowApiClient(new HttpClient(), NullLogger<MailcowApiClient>.Instance),
                NullCredentialProtector.Instance,
                NullLogger<MailcowProvisioningService>.Instance),
            dateTimeHelper);

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        // RedirectToAction resolves IUrlHelper; the test only cares about the persisted row.
        controller.Url = new Mock<IUrlHelper>().Object;
        return controller;
    }

    private static MailAccount SeedAccount(MailArchiverDbContext context, ProviderType provider,
        string? clientId, string? clientSecret, string? tenantId)
    {
        var account = new MailAccount
        {
            Name = "Switched account",
            EmailAddress = "someone@example.com",
            Provider = provider,
            ClientId = clientId,
            ClientSecret = clientSecret,
            TenantId = tenantId,
            Username = provider == ProviderType.MSA ? "someone@outlook.com" : null,
            UseSSL = true,
            LastSync = DateTime.UtcNow,
        };
        context.MailAccounts.Add(account);
        context.SaveChanges();
        return account;
    }

    private static MailAccountViewModel BuildMsaEditModel(MailAccount account, string? msaClientSecret = null)
        => new()
        {
            Id = account.Id,
            Name = account.Name,
            EmailAddress = account.EmailAddress,
            Provider = ProviderType.MSA,
            IsEnabled = true,
            MsaClientId = null,
            MsaClientSecret = msaClientSecret,
            ExcludedFolders = string.Empty,
        };

    [Fact]
    public async Task Edit_M365ToMsaWithDefaultClientId_DropsLeftoverClientSecret()
    {
        using var db = new SqliteDb();
        var account = SeedAccount(db.Context, ProviderType.M365, "m365-client-id", "m365-secret", "tenant-1");
        var controller = CreateController(db.Context, hasDefaultClientId: true);

        await controller.Edit(account.Id, BuildMsaEditModel(account));

        var stored = await db.Context.MailAccounts.AsNoTracking().SingleAsync(a => a.Id == account.Id);
        Assert.Equal(ProviderType.MSA, stored.Provider);
        Assert.Null(stored.ClientId);
        Assert.Null(stored.ClientSecret);
        Assert.Null(stored.TenantId);
    }

    [Fact]
    public async Task Edit_M365ToMsaWithoutDefaultClientId_DropsLeftoverClientSecret()
    {
        using var db = new SqliteDb();
        var account = SeedAccount(db.Context, ProviderType.M365, "m365-client-id", "m365-secret", "tenant-1");
        var controller = CreateController(db.Context, hasDefaultClientId: false);

        // Without a default client id the form requires an override; the M365 secret still must not survive.
        var model = BuildMsaEditModel(account, msaClientSecret: null);
        model.MsaClientId = "msa-override-client-id";

        await controller.Edit(account.Id, model);

        var stored = await db.Context.MailAccounts.AsNoTracking().SingleAsync(a => a.Id == account.Id);
        Assert.Equal("msa-override-client-id", stored.ClientId);
        Assert.Null(stored.ClientSecret);
    }

    [Fact]
    public async Task Edit_M365ToMsaWithNewSecret_StoresNewSecret()
    {
        using var db = new SqliteDb();
        var account = SeedAccount(db.Context, ProviderType.M365, "m365-client-id", "m365-secret", "tenant-1");
        var controller = CreateController(db.Context, hasDefaultClientId: true);

        await controller.Edit(account.Id, BuildMsaEditModel(account, msaClientSecret: "fresh-msa-secret"));

        var stored = await db.Context.MailAccounts.AsNoTracking().SingleAsync(a => a.Id == account.Id);
        Assert.Equal("fresh-msa-secret", stored.ClientSecret);
    }

    [Fact]
    public async Task Edit_MsaConfidentialClientKeepsItsOwnSecret()
    {
        using var db = new SqliteDb();
        var account = SeedAccount(db.Context, ProviderType.MSA, "msa-own-client-id", "msa-own-secret", null);
        var controller = CreateController(db.Context, hasDefaultClientId: false);

        var model = BuildMsaEditModel(account);
        model.MsaClientId = "msa-own-client-id";

        await controller.Edit(account.Id, model);

        var stored = await db.Context.MailAccounts.AsNoTracking().SingleAsync(a => a.Id == account.Id);
        Assert.Equal("msa-own-client-id", stored.ClientId);
        Assert.Equal("msa-own-secret", stored.ClientSecret);
    }

    [Fact]
    public async Task Edit_MsaFallingBackToDefaultClientId_DropsConfidentialSecret()
    {
        using var db = new SqliteDb();
        var account = SeedAccount(db.Context, ProviderType.MSA, "msa-own-client-id", "msa-own-secret", null);
        var controller = CreateController(db.Context, hasDefaultClientId: true);

        // Empty override → the shared public default client id is used, which takes no secret.
        await controller.Edit(account.Id, BuildMsaEditModel(account));

        var stored = await db.Context.MailAccounts.AsNoTracking().SingleAsync(a => a.Id == account.Id);
        Assert.Null(stored.ClientId);
        Assert.Null(stored.ClientSecret);
    }

    /// <summary>
    /// Minimal IServiceProvider used as RequestServices. The edit path under test resolves the
    /// authentication service only for audit logging; returning null keeps that branch inert.
    /// </summary>
    private sealed class ServiceCollectionShim : IServiceProvider, IServiceScopeFactory
    {
        private readonly IAuthenticationService _auth = BuildAdminAuth();

        private static IAuthenticationService BuildAdminAuth()
        {
            var auth = new Mock<IAuthenticationService>();
            auth.Setup(a => a.IsCurrentUserAdmin(It.IsAny<HttpContext>())).Returns(true);
            auth.Setup(a => a.IsCurrentUserSelfManager(It.IsAny<HttpContext>())).Returns(false);
            auth.Setup(a => a.GetCurrentUserDisplayName(It.IsAny<HttpContext>())).Returns("test-admin");
            return auth.Object;
        }

        private readonly ITempDataDictionaryFactory _tempData =
            new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionaryFactory(
                new Mock<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>().Object);

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IAuthenticationService)) return _auth;
            if (serviceType == typeof(ITempDataDictionaryFactory)) return _tempData;
            return null;
        }

        public IServiceScope CreateScope() => new NoopScope();

        private sealed class NoopScope : IServiceScope
        {
            public IServiceProvider ServiceProvider { get; } = new ServiceCollectionShim();
            public void Dispose() { }
        }
    }
}

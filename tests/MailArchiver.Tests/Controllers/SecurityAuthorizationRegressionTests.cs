using MailArchiver.Controllers;
using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Models.ViewModels;
using MailArchiver.Services;
using MailArchiver.Services.Factories;
using MailArchiver.Services.Mailcow;
using MailArchiver.Services.Providers;
using MailArchiver.Services.Security;
using MailArchiver.Tests.Infrastructure;
using MailArchiver.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace MailArchiver.Tests.Controllers;

/// <summary>
/// Regression tests for the authorization fixes from the 2026-11-19 security audit.
/// The restore and import paths used to validate only the TARGET account, so a restricted
/// user could copy or inject emails in an account they could not read. Account creation was
/// not role-gated at all. Each test drives the real controller against an in-memory SQLite
/// database and a mocked identity, and asserts that the request is refused before any
/// provider, job or session work happens.
/// </summary>
public class SecurityAuthorizationRegressionTests
{
    // ---------------------------------------------------------------- test doubles

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
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => Array.Empty<LocalizedString>();
    }

    private sealed class StubUrlHelper : IUrlHelper
    {
        public ActionContext ActionContext => new();
        public string? Action(UrlActionContext actionContext) => "/stub";
        public string? Content(string? contentPath) => contentPath;
        public bool IsLocalUrl(string? url) => true;
        public string? Link(string? routeName, object? values) => "/stub";
        public string? RouteUrl(UrlRouteContext routeContext) => "/stub";
    }

    private sealed class MemorySession : ISession
    {
        private readonly Dictionary<string, byte[]> _store = new();
        public bool IsAvailable => true;
        public string Id => "test-session";
        public IEnumerable<string> Keys => _store.Keys;
        public void Clear() => _store.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _store.Remove(key);
        public void Set(string key, byte[] value) => _store[key] = value;
        public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);
    }

    private sealed class TestSessionFeature : ISessionFeature
    {
        public ISession Session { get; set; } = new MemorySession();
    }

    /// <summary>
    /// RequestServices for the controllers under test. The controllers resolve the
    /// authentication and user services from here, so a test can switch between an admin,
    /// a self-manager and a standard user without rebuilding the controller.
    /// </summary>
    private sealed class TestServices : IServiceProvider, IServiceScopeFactory
    {
        public IAuthenticationService Auth { get; }
        public IUserService? Users { get; }

        public TestServices(IAuthenticationService auth, IUserService? users = null)
        {
            Auth = auth;
            Users = users;
        }

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IAuthenticationService)) return Auth;
            if (serviceType == typeof(IUserService)) return Users;
            return null;
        }

        public IServiceScope CreateScope() => new NoopScope(this);

        private sealed class NoopScope : IServiceScope
        {
            public NoopScope(IServiceProvider provider) => ServiceProvider = provider;
            public IServiceProvider ServiceProvider { get; }
            public void Dispose() { }
        }
    }

    // ---------------------------------------------------------------- identities

    private static IAuthenticationService Auth(string username, bool admin = false, bool selfManager = false)
    {
        var auth = new Mock<IAuthenticationService>();
        auth.Setup(a => a.IsCurrentUserAdmin(It.IsAny<HttpContext>())).Returns(admin);
        auth.Setup(a => a.IsCurrentUserSelfManager(It.IsAny<HttpContext>())).Returns(selfManager);
        auth.Setup(a => a.GetCurrentUserDisplayName(It.IsAny<HttpContext>())).Returns(username);
        return auth.Object;
    }

    private static IUserService UserWith(MailAccount account, int userId = 7, string username = "bob")
    {
        var users = new Mock<IUserService>();
        users.Setup(s => s.GetUserByUsernameAsync(username))
            .ReturnsAsync(new User { Id = userId, Username = username });
        users.Setup(s => s.GetUserMailAccountsAsync(userId))
            .ReturnsAsync(new List<MailAccount> { account });
        return users.Object;
    }

    // ---------------------------------------------------------------- data

    private static MailAccount NewAccount(string name) => new()
    {
        Name = name,
        EmailAddress = $"{Guid.NewGuid():N}@{name}.local",
        Username = $"user@{name}.local",
        Password = "x",
        Provider = ProviderType.IMPORT,
        IsEnabled = true,
        LastSync = DateTime.UtcNow
    };

    private static ArchivedEmail NewEmail(int accountId) => new()
    {
        MailAccountId = accountId,
        MessageId = $"<{Guid.NewGuid():N}@test.local>",
        Subject = "subject",
        Body = "body",
        HtmlBody = string.Empty,
        From = "sender@test.local",
        To = "rcpt@test.local",
        Cc = string.Empty,
        Bcc = string.Empty,
        FolderName = "INBOX",
        SentDate = DateTime.UtcNow.AddDays(-1),
        ReceivedDate = DateTime.UtcNow
    };

    private static (MailAccount Own, MailAccount Foreign, ArchivedEmail OwnEmail, ArchivedEmail ForeignEmail)
        SeedTwoAccounts(MailArchiverDbContext ctx)
    {
        var own = NewAccount("own");
        var foreign = NewAccount("foreign");
        ctx.MailAccounts.AddRange(own, foreign);
        ctx.SaveChanges();

        var ownEmail = NewEmail(own.Id);
        var foreignEmail = NewEmail(foreign.Id);
        ctx.ArchivedEmails.AddRange(ownEmail, foreignEmail);
        ctx.SaveChanges();

        return (own, foreign, ownEmail, foreignEmail);
    }

    // ---------------------------------------------------------------- controllers

    private static EmailsController CreateEmailsController(
        MailArchiverDbContext ctx,
        IAuthenticationService auth,
        IUserService? users = null)
    {
        var http = new DefaultHttpContext();
        http.RequestServices = new TestServices(auth, users);
        http.Features.Set<ISessionFeature>(new TestSessionFeature());

        var controller = new EmailsController(
            ctx,
            ServiceFactory.CreateEmailCoreService(ctx),
            providerFactory: null!,
            graphEmailService: null!,
            logger: NullLogger<EmailsController>.Instance,
            batchOptions: Options.Create(new BatchRestoreOptions()),
            selectionOptions: Options.Create(new SelectionOptions()),
            viewOptions: Options.Create(new ViewOptions()),
            deletionPolicy: Options.Create(new DeletionPolicyOptions()),
            localizer: new PassThroughLocalizer(),
            authService: auth);

        controller.ControllerContext = new ControllerContext { HttpContext = http };
        controller.Url = new StubUrlHelper();
        controller.TempData = new TempDataDictionary(http, Mock.Of<ITempDataProvider>());
        return controller;
    }

    private static MailAccountsController CreateMailAccountsController(
        MailArchiverDbContext ctx,
        IAuthenticationService auth,
        IUserService? users = null)
    {
        var services = new TestServices(auth, users);
        var http = new DefaultHttpContext { RequestServices = services };
        http.Features.Set<ISessionFeature>(new TestSessionFeature());
        var dateTimeHelper = new DateTimeHelper(Options.Create(new TimeZoneOptions()));

        var controller = new MailAccountsController(
            ctx,
            ServiceFactory.CreateEmailCoreService(ctx),
            new ProviderEmailServiceFactory(services, ctx, NullLogger<ProviderEmailServiceFactory>.Instance),
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
            Options.Create(new MsaOAuthOptions()),
            Mock.Of<IAccountStorageService>(),
            Options.Create(new CsvImportOptions()),
            Options.Create(new OffloadOptions()),
            Options.Create(new MailSyncOptions()),
            Mock.Of<IBatchRestoreService>(),
            new MailcowProvisioningService(
                ctx,
                new MailcowApiClient(new HttpClient(), NullLogger<MailcowApiClient>.Instance),
                NullCredentialProtector.Instance,
                NullLogger<MailcowProvisioningService>.Instance),
            dateTimeHelper);

        controller.ControllerContext = new ControllerContext { HttpContext = http };
        controller.Url = new StubUrlHelper();
        controller.TempData = new TempDataDictionary(http, Mock.Of<ITempDataProvider>());
        return controller;
    }

    // ---------------------------------------------------------------- C1: restore source authorization

    [Fact]
    public async Task Restore_NonAdminForeignEmail_ReturnsNotFound()
    {
        using var db = new SqliteDb();
        var (own, _, _, foreignEmail) = SeedTwoAccounts(db.Context);
        var controller = CreateEmailsController(db.Context, Auth("bob"), UserWith(own));

        var result = await controller.Restore(new EmailRestoreViewModel
        {
            EmailId = foreignEmail.Id,
            TargetAccountId = own.Id,
            TargetFolder = "INBOX"
        });

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task BatchRestore_NonAdminForeignEmail_DoesNotQueue()
    {
        using var db = new SqliteDb();
        var (own, _, _, foreignEmail) = SeedTwoAccounts(db.Context);
        var controller = CreateEmailsController(db.Context, Auth("bob"), UserWith(own));

        var result = await controller.BatchRestore(new BatchRestoreViewModel
        {
            SelectedEmailIds = new List<int> { foreignEmail.Id },
            TargetAccountId = own.Id,
            TargetFolder = "INBOX",
            ReturnUrl = "/Emails"
        });

        Assert.IsType<RedirectResult>(result);
        Assert.Contains("do not have access", controller.TempData["ErrorMessage"]?.ToString());
    }

    [Fact]
    public async Task BatchRestoreStart_NonAdminForeignEmail_DoesNotStoreSelection()
    {
        using var db = new SqliteDb();
        var (own, _, _, foreignEmail) = SeedTwoAccounts(db.Context);
        var controller = CreateEmailsController(db.Context, Auth("bob"), UserWith(own));

        var result = await controller.BatchRestoreStart(
            new List<int> { foreignEmail.Id }, returnUrl: "/Emails");

        Assert.IsType<RedirectResult>(result);
        Assert.Contains("do not have access", controller.TempData["ErrorMessage"]?.ToString());
        Assert.Null(controller.HttpContext.Session.GetString("BatchRestoreIds"));
    }

    [Fact]
    public async Task BatchRestoreStart_NonAdminOwnEmail_IsNotBlocked()
    {
        using var db = new SqliteDb();
        var (own, _, ownEmail, _) = SeedTwoAccounts(db.Context);
        var controller = CreateEmailsController(db.Context, Auth("bob"), UserWith(own));

        var result = await controller.BatchRestoreStart(
            new List<int> { ownEmail.Id }, returnUrl: "/Emails");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("BatchRestore", redirect.ActionName);
        Assert.NotNull(controller.HttpContext.Session.GetString("BatchRestoreIds"));
    }

    [Fact]
    public async Task StartAsyncBatchRestoreFromAccount_NonAdminForeignAccount_ReturnsNotFound()
    {
        using var db = new SqliteDb();
        var (own, foreign, _, _) = SeedTwoAccounts(db.Context);
        var controller = CreateEmailsController(db.Context, Auth("bob"), UserWith(own));

        var result = await controller.StartAsyncBatchRestoreFromAccount(foreign.Id);

        Assert.IsType<NotFoundResult>(result);
        Assert.Null(controller.HttpContext.Session.GetString("BatchRestoreIds"));
    }

    [Fact]
    public async Task StartAsyncBatchRestoreFromAccount_AdminForeignAccount_IsAllowed()
    {
        using var db = new SqliteDb();
        var (_, foreign, _, _) = SeedTwoAccounts(db.Context);
        var controller = CreateEmailsController(db.Context, Auth("admin", admin: true));

        // One email, below the async threshold: the action redirects to the restore form.
        var result = await controller.StartAsyncBatchRestoreFromAccount(foreign.Id);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("BatchRestore", redirect.ActionName);
    }

    // ---------------------------------------------------------------- H1: import target authorization

    [Fact]
    public async Task ImportMBox_NonAdminForeignAccount_ReturnsNotFound()
    {
        using var db = new SqliteDb();
        var (own, foreign, _, _) = SeedTwoAccounts(db.Context);
        var controller = CreateMailAccountsController(db.Context, Auth("bob"), UserWith(own));

        var result = await controller.ImportMBox(new MBoxImportViewModel
        {
            TargetAccountId = foreign.Id,
            TargetFolder = "INBOX",
            MBoxFile = new FormFile(new MemoryStream(new byte[16]), 0, 16, "MBoxFile", "mail.mbox")
        });

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ImportEml_NonAdminForeignAccount_ReturnsNotFound()
    {
        using var db = new SqliteDb();
        var (own, foreign, _, _) = SeedTwoAccounts(db.Context);
        var controller = CreateMailAccountsController(db.Context, Auth("bob"), UserWith(own));

        var result = await controller.ImportEml(new EmlImportViewModel
        {
            TargetAccountId = foreign.Id,
            EmlFile = new FormFile(new MemoryStream(new byte[16]), 0, 16, "EmlFile", "mail.eml")
        });

        Assert.IsType<NotFoundResult>(result);
    }

    // ---------------------------------------------------------------- H2: account creation is role-gated

    [Fact]
    public async Task CreateGet_StandardUser_IsForbidden()
    {
        using var db = new SqliteDb();
        var (own, _, _, _) = SeedTwoAccounts(db.Context);
        var controller = CreateMailAccountsController(db.Context, Auth("bob"), UserWith(own));

        var result = await controller.Create();

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task CreatePost_StandardUser_IsForbidden()
    {
        using var db = new SqliteDb();
        var (own, _, _, _) = SeedTwoAccounts(db.Context);
        var controller = CreateMailAccountsController(db.Context, Auth("bob"), UserWith(own));

        var result = await controller.Create(new CreateMailAccountViewModel
        {
            Name = "attacker",
            EmailAddress = "attacker@example.com",
            Provider = ProviderType.IMAP,
            ImapServer = "mail.attacker.example",
            ImapPort = 993,
            Username = "attacker",
            Password = "x",
            UseSSL = true,
            IsEnabled = true
        });

        Assert.IsType<ForbidResult>(result);
        Assert.Empty(db.Context.MailAccounts.Where(a => a.Name == "attacker"));
    }

    [Fact]
    public async Task CreateGet_SelfManager_IsAllowed()
    {
        using var db = new SqliteDb();
        var (own, _, _, _) = SeedTwoAccounts(db.Context);
        var controller = CreateMailAccountsController(
            db.Context, Auth("bob", selfManager: true), UserWith(own));

        var result = await controller.Create();

        Assert.IsType<ViewResult>(result);
    }
}

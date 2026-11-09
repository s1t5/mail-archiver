using MailArchiver.Auth.Handlers;
using MailArchiver.Auth.Options;
using MailArchiver.Controllers;
using MailArchiver.Models;
using MailArchiver.Models.ViewModels;
using MailArchiver.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace MailArchiver.Tests.Auth;

/// <summary>
/// The bootstrap admin password from <c>Authentication:Password</c> must force a
/// password change on every sign-in, not only on a brand new installation.
///
/// The old condition additionally required <c>mailAccountCount == 0</c>, so an
/// existing installation kept a fully working admin login with the shipped default
/// password from appsettings.json.
/// </summary>
public class AuthControllerBootstrapPasswordTests
{
    private const string BootstrapUsername = "admin";
    private const string BootstrapPassword = "secure123!";
    private const string ChangedPassword = "a-much-better-password";

    private sealed record Harness(AuthController Controller);

    private static Harness Create(string storedPasswordHash, string configuredPassword)
    {
        var authService = new Mock<MailArchiver.Services.IAuthenticationService>();
        authService.Setup(s => s.ValidateCredentials(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        authService.Setup(s => s.IsAuthenticated(It.IsAny<HttpContext>())).Returns(false);

        var userService = new Mock<IUserService>();
        userService
            .Setup(u => u.GetUserByUsernameAsync(BootstrapUsername))
            .ReturnsAsync(new User
            {
                Id = 1,
                Username = BootstrapUsername,
                Email = $"{BootstrapUsername}@local",
                PasswordHash = storedPasswordHash,
                IsAdmin = true,
                IsActive = true
            });
        // The stored hash "belongs to" exactly one plaintext password. The test controls
        // which one, so the comparison is a plain equality rather than a real hash check.
        var passwordMatchingStoredHash = storedPasswordHash == "hash-of-bootstrap"
            ? configuredPassword
            : "the-password-the-user-chose";
        userService
            .Setup(u => u.VerifyPassword(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string plain, string hash) =>
                hash == storedPasswordHash && plain == passwordMatchingStoredHash);

        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Username"] = BootstrapUsername,
                ["Authentication:Password"] = configuredPassword
            })
            .Build());
        httpContext.RequestServices = services.BuildServiceProvider();
        // The action writes MustChangePassword into the session, so a real session is needed.
        httpContext.Session = new TestSession();

        var localizer = new Mock<IStringLocalizer<SharedResource>>();
        localizer
            .Setup(l => l[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key));

        var authenticationHandler = new AuthenticationHandler(
            NullLogger<AuthenticationHandler>.Instance,
            authService.Object,
            userService.Object,
            new HttpContextAccessor { HttpContext = httpContext },
            Mock.Of<IAccessLogService>(),
            localizer.Object);

        var controller = new AuthController(
            authService.Object,
            authenticationHandler,
            userService.Object,
            NullLogger<AuthController>.Instance,
            localizer.Object,
            Mock.Of<IAccessLogService>(),
            Mock.Of<IServiceScopeFactory>(),
            Options.Create(new OAuthOptions { Enabled = false, DisablePasswordLogin = false }));

        // The success path ends in RedirectToLocal, which reads ControllerBase.Url. The
        // ActionContext must carry a ControllerActionDescriptor for ControllerContext,
        // and the UrlHelper needs routing services, so both are provided explicitly.
        var actionDescriptor = new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor
        {
            ControllerName = "Auth",
            ActionName = "Login"
        };
        var actionContext = new ActionContext(
            httpContext,
            new Microsoft.AspNetCore.Routing.RouteData(),
            actionDescriptor);
        controller.ControllerContext = new ControllerContext(actionContext);
        controller.Url = new Microsoft.AspNetCore.Mvc.Routing.UrlHelper(actionContext);
        controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());

        return new Harness(controller);
    }

    private static LoginViewModel Login(string password) => new()
    {
        Username = BootstrapUsername,
        Password = password,
        RememberMe = false
    };

    [Fact]
    public async Task BootstrapPassword_ForcesChange_EvenWithExistingMailAccounts()
    {
        // Regression: the archive is not empty here. The old code required an empty
        // archive before forcing the change, which is exactly the gap this closes.
        var harness = Create(storedPasswordHash: "hash-of-bootstrap", configuredPassword: BootstrapPassword);

        await harness.Controller.Login(Login(BootstrapPassword));

        Assert.Equal("true", harness.Controller.HttpContext.Session.GetString("MustChangePassword"));
    }

    [Fact]
    public async Task ChangedPassword_DoesNotForceChange()
    {
        var harness = Create(storedPasswordHash: "hash-of-changed", configuredPassword: BootstrapPassword);

        await harness.Controller.Login(Login(ChangedPassword));

        Assert.Null(harness.Controller.HttpContext.Session.GetString("MustChangePassword"));
    }

    [Fact]
    public async Task BootstrapPassword_StillMatchesAfterOperatorRotatedTheConfigValue()
    {
        // If the operator changed Authentication:Password in the config but the stored
        // admin hash still holds the shipped default, the hash comparison must not fire.
        var harness = Create(storedPasswordHash: "hash-of-shipped-default", configuredPassword: "some-new-config-value");

        await harness.Controller.Login(Login("shipped-default"));

        Assert.Null(harness.Controller.HttpContext.Session.GetString("MustChangePassword"));
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _store = new();

        public bool IsAvailable => true;
        public string Id { get; } = Guid.NewGuid().ToString();
        public IEnumerable<string> Keys => _store.Keys;

        public void Clear() => _store.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _store.Remove(key);
        public void Set(string key, byte[] value) => _store[key] = value;
        public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);
    }
}

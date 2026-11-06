using MailArchiver.Auth.Handlers;
using MailArchiver.Auth.Options;
using MailArchiver.Controllers;
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
/// Regression tests for the OAuth-only sign-in policy.
///
/// <c>OAuth:DisablePasswordLogin=true</c> used to be enforced only by hiding the
/// password form in the login view. The POST action validated local credentials
/// regardless, so a direct POST with a valid local password still produced an auth
/// cookie and bypassed the configured policy. These tests pin the server-side check.
/// </summary>
public class AuthControllerPasswordLoginDisabledTests
{
    private const string ValidUsername = "admin";
    private const string ValidPassword = "correct-horse-battery-staple";

    private sealed record Harness(
        AuthController Controller,
        Mock<MailArchiver.Services.IAuthenticationService> AuthService);

    private static Harness Create(bool oAuthEnabled, bool disablePasswordLogin, bool passwordIsValid)
    {
        var authService = new Mock<MailArchiver.Services.IAuthenticationService>();
        authService
            .Setup(s => s.ValidateCredentials(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(passwordIsValid);
        authService.Setup(s => s.IsAuthenticated(It.IsAny<HttpContext>())).Returns(false);

        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;

        // Minimal service provider so the action can resolve IConfiguration and the
        // DbContext if it ever reaches the post-sign-in branch. Reaching that branch is
        // a failure in the rejection tests, so nothing needs to be registered for it.
        // The MVC routing services are required because the rejection path returns
        // RedirectToAction, which needs an IUrlHelper to build the target URL.
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLogging();
        services.AddRouting();
        services.AddControllersWithViews();
        httpContext.RequestServices = services.BuildServiceProvider();

        var localizer = new Mock<IStringLocalizer<SharedResource>>();
        localizer
            .Setup(l => l[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key));

        var authenticationHandler = new AuthenticationHandler(
            NullLogger<AuthenticationHandler>.Instance,
            authService.Object,
            Mock.Of<IUserService>(),
            new HttpContextAccessor { HttpContext = httpContext },
            Mock.Of<IAccessLogService>(),
            localizer.Object);

        var controller = new AuthController(
            authService.Object,
            authenticationHandler,
            Mock.Of<IUserService>(),
            NullLogger<AuthController>.Instance,
            localizer.Object,
            Mock.Of<IAccessLogService>(),
            Mock.Of<IServiceScopeFactory>(),
            Options.Create(new OAuthOptions
            {
                Enabled = oAuthEnabled,
                DisablePasswordLogin = disablePasswordLogin,
                AutoRedirect = true,
                Authority = "https://idp.invalid",
                ClientId = "test-client",
                ClientSecret = "test-secret"
            }));

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());

        return new Harness(controller, authService);
    }

    private static LoginViewModel ValidLogin() => new()
    {
        Username = ValidUsername,
        Password = ValidPassword,
        RememberMe = false
    };

    [Fact]
    public async Task Post_WithValidPassword_IsRejectedWhenPasswordLoginDisabled()
    {
        var harness = Create(oAuthEnabled: true, disablePasswordLogin: true, passwordIsValid: true);

        var result = await harness.Controller.Login(ValidLogin());

        // The credential check must never run, otherwise a cookie could be issued.
        harness.AuthService.Verify(
            s => s.ValidateCredentials(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);

        var redirect = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/Auth/Login?error=PasswordLoginDisabled", redirect.Url);

        // No auth cookie must be written for the response.
        Assert.False(harness.Controller.HttpContext.Response.Headers.ContainsKey("Set-Cookie"));
    }

    [Fact]
    public async Task Post_IsRejectedEvenWhenModelStateIsInvalid()
    {
        // The policy check runs before model validation, so a malformed POST cannot
        // slip past by failing validation first.
        var harness = Create(oAuthEnabled: true, disablePasswordLogin: true, passwordIsValid: true);
        harness.Controller.ModelState.AddModelError("Username", "required");

        var result = await harness.Controller.Login(new LoginViewModel());

        harness.AuthService.Verify(
            s => s.ValidateCredentials(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
        Assert.IsType<LocalRedirectResult>(result);
    }

    [Fact]
    public async Task Post_ReachesCredentialCheckWhenPasswordLoginEnabled()
    {
        // Guard against over-blocking: with the policy off, password login is normal.
        var harness = Create(oAuthEnabled: true, disablePasswordLogin: false, passwordIsValid: false);

        var result = await harness.Controller.Login(ValidLogin());

        harness.AuthService.Verify(
            s => s.ValidateCredentials(ValidUsername, ValidPassword),
            Times.Once);
        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task Post_ReachesCredentialCheckWhenOAuthDisabled()
    {
        // DisablePasswordLogin without OAuth must not lock everyone out.
        var harness = Create(oAuthEnabled: false, disablePasswordLogin: true, passwordIsValid: false);

        await harness.Controller.Login(ValidLogin());

        harness.AuthService.Verify(
            s => s.ValidateCredentials(ValidUsername, ValidPassword),
            Times.Once);
    }
}

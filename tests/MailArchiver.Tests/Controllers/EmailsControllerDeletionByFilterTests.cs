using MailArchiver.Controllers;
using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Models.ViewModels;
using MailArchiver.Services;
using MailArchiver.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Text.Json;
using Xunit;

namespace MailArchiver.Tests.Controllers;

/// <summary>
/// Covers the guards of the filter-based bulk deletion: the preview counts, the cap,
/// the confirmation count, the requirement for a narrowing filter and the account
/// restriction. The job itself is covered by EmailDeletionServiceTests; these tests
/// stop at the queue boundary.
/// </summary>
[Collection(TestDbFixture.CollectionName)]
public class EmailsControllerDeletionByFilterTests
{
    private readonly TestDbFixture _fixture;

    public EmailsControllerDeletionByFilterTests(TestDbFixture fixture) => _fixture = fixture;

    private static IStringLocalizer<SharedResource> BuildLocalizer()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization(options => options.ResourcesPath = "Resources");
        return services.BuildServiceProvider().GetRequiredService<IStringLocalizer<SharedResource>>();
    }

    private static Mock<IAuthenticationService> AdminAuth()
    {
        var auth = new Mock<IAuthenticationService>();
        auth.Setup(a => a.IsCurrentUserAdmin(It.IsAny<HttpContext>())).Returns(true);
        auth.Setup(a => a.GetCurrentUserDisplayName(It.IsAny<HttpContext>())).Returns("admin");
        return auth;
    }

    private static EmailsController CreateController(
        MailArchiverDbContext ctx,
        DeletionPolicyOptions policy,
        IAuthenticationService auth,
        IEmailDeletionService? deletionService = null,
        IUserService? userService = null)
    {
        var http = new DefaultHttpContext();
        var services = new ServiceCollection();
        if (userService != null)
        {
            services.AddSingleton(userService);
        }
        http.RequestServices = services.BuildServiceProvider();

        var controller = new EmailsController(
            ctx,
            ServiceFactory.CreateEmailCoreService(ctx),
            providerFactory: null!,
            graphEmailService: null!,
            logger: NullLogger<EmailsController>.Instance,
            batchOptions: Options.Create(new BatchRestoreOptions()),
            selectionOptions: Options.Create(new SelectionOptions()),
            viewOptions: Options.Create(new ViewOptions()),
            deletionPolicy: Options.Create(policy),
            localizer: BuildLocalizer(),
            authService: auth,
            emailDeletionService: deletionService);

        controller.ControllerContext = new ControllerContext { HttpContext = http };
        controller.Url = new StubUrlHelper();
        controller.TempData = new TempDataDictionary(http, Mock.Of<ITempDataProvider>());
        return controller;
    }

    /// <summary>RedirectToAction asks the Url helper; the tests only assert the action and route values.</summary>
    private sealed class StubUrlHelper : Microsoft.AspNetCore.Mvc.IUrlHelper
    {
        public Microsoft.AspNetCore.Mvc.ActionContext ActionContext => new();

        public string? Action(Microsoft.AspNetCore.Mvc.Routing.UrlActionContext actionContext) => "/stub";

        public string? Content(string? contentPath) => contentPath;

        public bool IsLocalUrl(string? url) => true;

        public string? Link(string? routeName, object? values) => "/stub";

        public string? RouteUrl(Microsoft.AspNetCore.Mvc.Routing.UrlRouteContext routeContext) => "/stub";
    }

    private static MailAccount NewAccount() => new()
    {
        Name = "ctl-" + Guid.NewGuid().ToString("N")[..8],
        EmailAddress = $"{Guid.NewGuid():N}@ctl.local",
        Username = "ctl@ctl.local",
        Password = "x",
        Provider = ProviderType.IMPORT,
        IsEnabled = false,
        LastSync = DateTime.UtcNow
    };

    private static ArchivedEmail NewEmail(int accountId, string subject, bool isLocked = false) => new()
    {
        MailAccountId = accountId,
        MessageId = $"<{Guid.NewGuid():N}@ctl.local>",
        Subject = subject,
        Body = "body",
        HtmlBody = string.Empty,
        From = "sender@ctl.local",
        To = "rcpt@ctl.local",
        Cc = string.Empty,
        Bcc = string.Empty,
        FolderName = "INBOX",
        SentDate = DateTime.UtcNow.AddDays(-1),
        ReceivedDate = DateTime.UtcNow,
        IsLocked = isLocked
    };

    private static async Task<(MailAccount Account, List<ArchivedEmail> Matching)> SeedAsync(
        MailArchiverDbContext ctx, int matching, int locked = 0, int other = 0)
    {
        var account = NewAccount();
        ctx.MailAccounts.Add(account);
        await ctx.SaveChangesAsync();

        var matches = Enumerable.Range(0, matching)
            .Select(i => NewEmail(account.Id, $"invoice {i}"))
            .ToList();
        var lockedEmails = Enumerable.Range(0, locked)
            .Select(i => NewEmail(account.Id, $"invoice locked {i}", isLocked: true))
            .ToList();
        var others = Enumerable.Range(0, other)
            .Select(i => NewEmail(account.Id, $"newsletter {i}"))
            .ToList();

        ctx.ArchivedEmails.AddRange(matches);
        ctx.ArchivedEmails.AddRange(lockedEmails);
        ctx.ArchivedEmails.AddRange(others);
        await ctx.SaveChangesAsync();

        return (account, matches);
    }

    private static SearchViewModel FilterFor(MailAccount account, string term = "invoice") => new()
    {
        SearchTerm = term,
        SelectedAccountId = account.Id
    };

    [Fact]
    public async Task Preview_ReturnsDeletableLockedAndCap()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        var (account, _) = await SeedAsync(ctx, matching: 2, locked: 1, other: 1);
        var policy = new DeletionPolicyOptions { MaxBulkDeleteEmails = 1234 };
        var controller = CreateController(ctx, policy, AdminAuth().Object);

        var result = await controller.DeletionPreview(FilterFor(account));

        var json = Assert.IsType<JsonResult>(result);
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(json.Value));
        Assert.Equal(2, doc.RootElement.GetProperty("deletable").GetInt32());
        Assert.Equal(1, doc.RootElement.GetProperty("locked").GetInt32());
        Assert.Equal(1234, doc.RootElement.GetProperty("max").GetInt32());

        await scope.RollbackAsync();
    }

    [Fact]
    public async Task DeleteByFilter_WhenPolicyDisablesDeletion_DoesNotQueue()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        var (account, _) = await SeedAsync(ctx, matching: 2);
        var deletion = new Mock<IEmailDeletionService>();
        var controller = CreateController(
            ctx, new DeletionPolicyOptions { DeletionAllowed = false }, AdminAuth().Object, deletion.Object);

        var result = await controller.DeleteByFilter(FilterFor(account), confirmCount: 2, returnUrl: "/Emails");

        Assert.IsType<RedirectResult>(result);
        Assert.NotNull(controller.TempData["ErrorMessage"]);
        deletion.Verify(s => s.QueueJob(It.IsAny<EmailDeletionJob>()), Times.Never);

        await scope.RollbackAsync();
    }

    [Fact]
    public async Task DeleteByFilter_WithoutNarrowingFilter_DoesNotQueue()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        await SeedAsync(ctx, matching: 2);
        var deletion = new Mock<IEmailDeletionService>();
        var controller = CreateController(ctx, new DeletionPolicyOptions(), AdminAuth().Object, deletion.Object);

        // Direction alone is not a narrowing criterion.
        var model = new SearchViewModel { IsOutgoing = true };

        var result = await controller.DeleteByFilter(model, confirmCount: 2, returnUrl: "/Emails");

        Assert.IsType<RedirectResult>(result);
        Assert.NotNull(controller.TempData["ErrorMessage"]);
        deletion.Verify(s => s.QueueJob(It.IsAny<EmailDeletionJob>()), Times.Never);

        await scope.RollbackAsync();
    }

    [Fact]
    public async Task DeleteByFilter_AboveCap_DoesNotQueue()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        var (account, _) = await SeedAsync(ctx, matching: 3);
        var deletion = new Mock<IEmailDeletionService>();
        var controller = CreateController(
            ctx, new DeletionPolicyOptions { MaxBulkDeleteEmails = 2 }, AdminAuth().Object, deletion.Object);

        var result = await controller.DeleteByFilter(FilterFor(account), confirmCount: 3, returnUrl: "/Emails");

        Assert.IsType<RedirectResult>(result);
        Assert.NotNull(controller.TempData["ErrorMessage"]);
        deletion.Verify(s => s.QueueJob(It.IsAny<EmailDeletionJob>()), Times.Never);

        await scope.RollbackAsync();
    }

    [Fact]
    public async Task DeleteByFilter_WhenCountChanged_DoesNotQueue()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        var (account, _) = await SeedAsync(ctx, matching: 2);
        var deletion = new Mock<IEmailDeletionService>();
        var controller = CreateController(ctx, new DeletionPolicyOptions(), AdminAuth().Object, deletion.Object);

        var result = await controller.DeleteByFilter(FilterFor(account), confirmCount: 1, returnUrl: "/Emails");

        Assert.IsType<RedirectResult>(result);
        Assert.NotNull(controller.TempData["ErrorMessage"]);
        deletion.Verify(s => s.QueueJob(It.IsAny<EmailDeletionJob>()), Times.Never);

        await scope.RollbackAsync();
    }

    [Fact]
    public async Task DeleteByFilter_QueuesFrozenFilterAndRedirectsToStatus()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        var (account, matching) = await SeedAsync(ctx, matching: 2, other: 1);
        EmailDeletionJob? queued = null;
        var deletion = new Mock<IEmailDeletionService>();
        deletion.Setup(s => s.QueueJob(It.IsAny<EmailDeletionJob>()))
            .Callback<EmailDeletionJob>(job => queued = job)
            .Returns("job-1");
        var controller = CreateController(ctx, new DeletionPolicyOptions(), AdminAuth().Object, deletion.Object);

        var result = await controller.DeleteByFilter(FilterFor(account), confirmCount: 2, returnUrl: "/Emails");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("EmailDeletionStatus", redirect.ActionName);
        Assert.Equal("job-1", redirect.RouteValues!["jobId"]);

        Assert.NotNull(queued);
        Assert.Equal(2, queued!.TotalEmails);
        Assert.Equal("admin", queued.UserId);
        Assert.NotNull(queued.Filter);
        Assert.Equal(matching.Max(e => e.Id), queued.Filter!.UpperBoundId);
        Assert.Contains("term:invoice", queued.Filter.Description);
        Assert.Equal(account.Id, queued.Filter.AccountId);

        await scope.RollbackAsync();
    }

    [Fact]
    public async Task DeleteByFilter_NonAdminWithoutAccounts_MatchesNothing()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        var (account, _) = await SeedAsync(ctx, matching: 2);

        var auth = new Mock<IAuthenticationService>();
        auth.Setup(a => a.IsCurrentUserAdmin(It.IsAny<HttpContext>())).Returns(false);
        auth.Setup(a => a.GetCurrentUserDisplayName(It.IsAny<HttpContext>())).Returns("bob");

        var userService = new Mock<IUserService>();
        userService.Setup(s => s.GetUserByUsernameAsync("bob"))
            .ReturnsAsync(new User { Id = 4242, Username = "bob" });
        userService.Setup(s => s.GetUserMailAccountsAsync(4242))
            .ReturnsAsync(new List<MailAccount>());

        var deletion = new Mock<IEmailDeletionService>();
        var controller = CreateController(
            ctx, new DeletionPolicyOptions(), auth.Object, deletion.Object, userService.Object);

        var result = await controller.DeleteByFilter(FilterFor(account), confirmCount: 2, returnUrl: "/Emails");

        Assert.IsType<RedirectResult>(result);
        Assert.NotNull(controller.TempData["ErrorMessage"]);
        deletion.Verify(s => s.QueueJob(It.IsAny<EmailDeletionJob>()), Times.Never);

        await scope.RollbackAsync();
    }
}

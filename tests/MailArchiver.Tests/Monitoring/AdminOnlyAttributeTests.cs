using System.Security.Claims;
using MailArchiver.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace MailArchiver.Tests.Monitoring;

public class AdminOnlyAttributeTests
{
    private static ActionExecutingContext Context(params Claim[] claims)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
        };
        httpContext.Request.Path = "/api/v1/metrics";

        var actionContext = new ActionContext(
            httpContext, new RouteData(), new ActionDescriptor());

        return new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            controller: new object());
    }

    [Fact]
    public void AdminRole_PassesThrough()
    {
        var context = Context(new Claim(ClaimTypes.Role, "Admin"));

        new AdminOnlyAttribute().OnActionExecuting(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void NonAdmin_Returns403ProblemWithoutRedirect()
    {
        var context = Context(new Claim(ClaimTypes.Role, "SelfManager"));

        new AdminOnlyAttribute().OnActionExecuting(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Contains("application/problem+json", result.ContentTypes);

        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.Status);
        Assert.Equal("/api/v1/metrics", problem.Instance);
    }

    [Fact]
    public void MissingIdentity_Returns403()
    {
        var context = Context();

        new AdminOnlyAttribute().OnActionExecuting(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
    }
}

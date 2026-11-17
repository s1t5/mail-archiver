using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MailArchiver.Attributes;

/// <summary>
/// Requires the Admin role for an API action and answers with a JSON problem instead of
/// the cookie scheme's redirect to /Auth/AccessDenied. The role claim is set by the API key
/// handler, so this filter applies to keyed /api/ requests.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class AdminOnlyAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.HttpContext.User?.IsInRole("Admin") == true)
        {
            return;
        }

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Administrator role required.",
            Detail = "The API key used for this request does not belong to an administrator.",
            Instance = context.HttpContext.Request.Path.Value
        };

        context.Result = new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status403Forbidden,
            ContentTypes = { "application/problem+json" }
        };
    }
}

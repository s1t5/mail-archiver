using MailArchiver.Models;
using MailArchiver.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace MailArchiver.Controllers.Api;

[ApiController]
[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("Api")]
public abstract class ApiControllerBase : ControllerBase
{
    // Returns null for admins (all accounts), a list of allowed account IDs for
    // restricted users, or an empty list when the user has no access. Delegates
    // to IAccountAccessResolver — the single source of truth shared with the
    // MCP server, so permission logic cannot drift between the two surfaces.
    protected async Task<List<int>?> GetAllowedAccountIdsAsync()
        => await HttpContext.RequestServices.GetRequiredService<IAccountAccessResolver>()
            .GetAllowedAccountIdsAsync(HttpContext);

    /// <summary>Display name of the authenticated API-key owner, for access logs.</summary>
    protected string CurrentUsername
        => HttpContext.RequestServices.GetRequiredService<IAuthenticationService>()
            .GetCurrentUserDisplayName(HttpContext);

    /// <summary>Database id of the authenticated API-key owner, or null.</summary>
    protected int? CurrentUserId
        => HttpContext.RequestServices.GetRequiredService<IAuthenticationService>()
            .GetCurrentUserId(HttpContext);

    protected bool IsAdmin => User.IsInRole("Admin");

    protected bool IsSelfManager => User.IsInRole("SelfManager");

    /// <summary>True when the presented API key carries the given scope.</summary>
    protected bool HasScope(string scope) => User.HasClaim(ApiKeyScopes.ClaimType, scope);

    /// <summary>
    /// Guard for every non-GET endpoint. Returns a 403 problem when the write API
    /// is disabled deployment-wide (<c>Api:AllowWrites</c>) or when the presented
    /// key does not carry the <c>write</c> scope; null when the call may proceed.
    /// </summary>
    protected ObjectResult? RequireWriteAccess()
    {
        var options = HttpContext.RequestServices.GetRequiredService<IOptions<ApiOptions>>().Value;

        if (!options.AllowWrites)
        {
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Write access is disabled.",
                detail: "This deployment runs the REST API read-only. Set Api:AllowWrites=true to enable the write endpoints.");
        }

        if (!HasScope(ApiKeyScopes.Write))
        {
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Insufficient scope.",
                detail: "This API key is read-only. Use a key created with the 'write' scope.");
        }

        return null;
    }

    /// <summary>
    /// Guard for endpoints that delete archived mail. The deployment-level
    /// deletion policy wins over the API configuration: when
    /// <c>DeletionPolicy:DeletionAllowed</c> is false, deletion is refused with
    /// 409 even for an administrator key.
    /// </summary>
    protected ObjectResult? RequireDeletionAllowed()
    {
        var policy = HttpContext.RequestServices.GetRequiredService<IOptions<DeletionPolicyOptions>>().Value;

        if (!policy.DeletionAllowed)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Deletion is disabled by policy.",
                detail: "DeletionPolicy:DeletionAllowed is false for this deployment, so archived mail cannot be deleted.");
        }

        return null;
    }

    /// <summary>
    /// Guard for account writes: mirrors the web UI, where account management is
    /// limited to administrators and self-managers.
    /// </summary>
    protected ObjectResult? RequireAccountManager()
    {
        if (IsAdmin || IsSelfManager)
        {
            return null;
        }

        return Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "Insufficient permissions.",
            detail: "Managing mail accounts requires the Admin or SelfManager role.");
    }
}

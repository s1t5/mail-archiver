using MailArchiver.Services;
using Microsoft.AspNetCore.Http;

namespace MailArchiver.Mcp;

/// <summary>
/// Base class for MCP tool types. Provides the same account-access scoping helper
/// that <see cref="MailArchiver.Controllers.Api.ApiControllerBase.GetAllowedAccountIdsAsync"/>
/// uses for the REST API, so MCP tools honour the exact same per-user permissions:
/// admins see all accounts, restricted users see only their assigned accounts,
/// and users without any assignment see nothing.
/// </summary>
public abstract class McpToolBase
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    protected McpToolBase(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected HttpContext HttpContext =>
        _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("MCP tool invoked without an HTTP context.");

    protected string CurrentUsername
    {
        get
        {
            var authService = HttpContext.RequestServices.GetService<IAuthenticationService>();
            return authService?.GetCurrentUserDisplayName(HttpContext) ?? "mcp-anonymous";
        }
    }

    /// <summary>
    /// Returns null for admins (all accounts), a list of allowed account IDs for
    /// restricted users, or an empty list when the user has no access. Mirrors
    /// ApiControllerBase.GetAllowedAccountIdsAsync exactly.
    /// </summary>
    protected async Task<List<int>?> GetAllowedAccountIdsAsync()
    {
        var authService = HttpContext.RequestServices.GetService<IAuthenticationService>();
        var userService = HttpContext.RequestServices.GetService<IUserService>();

        if (authService == null || userService == null || authService.IsCurrentUserAdmin(HttpContext))
        {
            return null;
        }

        var username = authService.GetCurrentUserDisplayName(HttpContext);
        var user = await userService.GetUserByUsernameAsync(username);
        if (user == null)
        {
            return new List<int>();
        }

        var userAccounts = await userService.GetUserMailAccountsAsync(user.Id);
        return userAccounts.Select(a => a.Id).ToList();
    }
}
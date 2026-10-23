using MailArchiver.Services;
using Microsoft.AspNetCore.Http;

namespace MailArchiver.Mcp;

/// <summary>
/// Base class for MCP tool types. Provides the same account-access scoping helper
/// that <see cref="MailArchiver.Controllers.Api.ApiControllerBase.GetAllowedAccountIdsAsync"/>
/// uses for the REST API, so MCP tools honour the exact same per-user permissions:
/// admins see all accounts, restricted users see only their assigned accounts,
/// and users without any assignment see nothing. Both surfaces delegate to the
/// shared <see cref="IAccountAccessResolver"/> — single source of truth.
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
    /// restricted users, or an empty list when the user has no access. Delegates
    /// to <see cref="IAccountAccessResolver"/> — the same instance the REST API
    /// uses — so MCP and REST permissions cannot drift apart.
    /// </summary>
    protected async Task<List<int>?> GetAllowedAccountIdsAsync()
        => await HttpContext.RequestServices.GetRequiredService<IAccountAccessResolver>()
            .GetAllowedAccountIdsAsync(HttpContext);
}
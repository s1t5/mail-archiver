using Microsoft.AspNetCore.Http;

namespace MailArchiver.Services
{
    /// <summary>
    /// Single source of truth for per-user account-access scoping. Both the
    /// REST API (<see cref="MailArchiver.Controllers.Api.ApiControllerBase"/>) and
    /// the MCP server (<see cref="MailArchiver.Mcp.McpToolBase"/>) delegate to
    /// this resolver so that permission logic lives in exactly one place.
    /// </summary>
    /// <remarks>
    /// Returns null for admins (all accounts), a list of allowed account IDs
    /// for restricted users, or an empty list when the user has no access.
    /// Fails closed with <see cref="InvalidOperationException"/> when the
    /// underlying <see cref="IAuthenticationService"/> or <see cref="IUserService"/>
    /// cannot be resolved, so a misconfiguration can never silently escalate
    /// to admin scope.
    /// </remarks>
    public interface IAccountAccessResolver
    {
        Task<List<int>?> GetAllowedAccountIdsAsync(HttpContext context);
    }
}
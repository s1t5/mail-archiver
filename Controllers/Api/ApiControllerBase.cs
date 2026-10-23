using MailArchiver.Services;
using Microsoft.AspNetCore.Mvc;

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
}

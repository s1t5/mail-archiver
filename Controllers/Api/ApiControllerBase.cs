using MailArchiver.Services;
using Microsoft.AspNetCore.Mvc;

namespace MailArchiver.Controllers.Api;

[ApiController]
[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("Api")]
public abstract class ApiControllerBase : ControllerBase
{
    // Returns null for admins (all accounts), a list of allowed account IDs for
    // restricted users, or an empty list when the user has no access.
    protected async Task<List<int>?> GetAllowedAccountIdsAsync()
    {
        var authService = HttpContext.RequestServices.GetService<MailArchiver.Services.IAuthenticationService>();
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

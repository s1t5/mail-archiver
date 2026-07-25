using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace MailArchiver.Services
{
    /// <summary>
    /// Default implementation of <see cref="IAccountAccessResolver"/>. Mirrors
    /// the original logic of <c>ApiControllerBase.GetAllowedAccountIdsAsync</c>
    /// and <c>McpToolBase.GetAllowedAccountIdsAsync</c>, consolidated into a
    /// single source of truth.
    /// </summary>
    public class AccountAccessResolver : IAccountAccessResolver
    {
        public async Task<List<int>?> GetAllowedAccountIdsAsync(HttpContext context)
        {
            var authService = context.RequestServices.GetService<IAuthenticationService>();
            var userService = context.RequestServices.GetService<IUserService>();

            // Fail closed: if the required services are unavailable (which
            // would indicate a serious DI misconfiguration), never fall back
            // to null (= admin scope). Throw so the operator notices.
            if (authService == null || userService == null)
            {
                throw new InvalidOperationException(
                    "Account access resolver services unavailable: IAuthenticationService or IUserService is not registered.");
            }

            if (authService.IsCurrentUserAdmin(context))
            {
                return null;
            }

            var username = authService.GetCurrentUserDisplayName(context);
            var user = await userService.GetUserByUsernameAsync(username);
            if (user == null)
            {
                return new List<int>();
            }

            var userAccounts = await userService.GetUserMailAccountsAsync(user.Id);
            return userAccounts.Select(a => a.Id).ToList();
        }
    }
}
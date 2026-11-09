using MailArchiver.Auth.Helpers;
using MailArchiver.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace MailArchiver.Auth.Services
{
    /// <summary>
    /// Validates an auth cookie against the current user row. ASP.NET Core cookie
    /// authentication is self-contained: without this check a cookie stays valid until
    /// it expires, even after the password changed, the account was deactivated or the
    /// user was deleted. The security stamp ties every cookie to the state it was
    /// issued for.
    /// </summary>
    public static class SecurityStampValidator
    {
        public static async Task<bool> IsSessionValidAsync(ClaimsPrincipal? principal, MailArchiverDbContext dbContext)
        {
            var stamp = principal?.FindFirst(AuthClaimTypes.SecurityStamp)?.Value;
            var userIdValue = principal?.FindFirst(AuthClaimTypes.UserId)?.Value;

            if (string.IsNullOrEmpty(stamp) || !int.TryParse(userIdValue, out var userId))
            {
                return false;
            }

            var current = await dbContext.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.SecurityStamp, u.IsActive })
                .FirstOrDefaultAsync();

            return current != null
                && current.IsActive
                && string.Equals(current.SecurityStamp, stamp, StringComparison.Ordinal);
        }
    }
}

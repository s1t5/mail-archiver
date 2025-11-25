using MailArchiver.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MailArchiver.Attributes
{
    public class UserAccessRequiredAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var authService = context.HttpContext.RequestServices.GetService<MailArchiver.Services.IAuthenticationService>();
            
            // First check if user is authenticated
            if (authService == null || !authService.IsAuthenticated(context.HttpContext))
            {
                // Redirect to login page
                var returnUrl = context.HttpContext.Request.Path + context.HttpContext.Request.QueryString;
                context.Result = new RedirectToActionResult("Login", "Auth", new { returnUrl });
                return;
            }
            
            // For admin users, allow access
            var isAdmin = authService.IsCurrentUserAdmin(context.HttpContext);
            var username = authService.GetCurrentUserDisplayName(context.HttpContext);
            
            if (isAdmin)
            {
                var logger = context.HttpContext.RequestServices.GetService<ILogger<UserAccessRequiredAttribute>>();
                if (logger != null)
                {
                    logger.LogDebug("User {Username} is admin, granting access", username);
                }
                base.OnActionExecuting(context);
                return;
            }
            
            // For regular users, check if they're trying to access a specific account
            // and if they have access to that account
            var userService = context.HttpContext.RequestServices.GetService<IUserService>();
            if (userService == null)
            {
                context.Result = new RedirectToActionResult("AccessDenied", "Auth", null);
                return;
            }
            
            // Get the account ID from the route or query parameters
            var accountId = GetAccountId(context);
            
            // If no specific account is being accessed, allow general access
            // (but we'll filter the results in the controller)
            if (!accountId.HasValue)
            {
                base.OnActionExecuting(context);
                return;
            }
            
            // Check if the user has access to this specific account
            var currentUser = authService.GetCurrentUserDisplayName(context.HttpContext);
            var user = userService.GetUserByUsernameAsync(currentUser).Result;
            
            if (user == null)
            {
                context.Result = new RedirectToActionResult("AccessDenied", "Auth", null);
                return;
            }
            
            var hasAccess = userService.IsUserAuthorizedForAccountAsync(user.Id, accountId.Value).Result;
            
            if (!hasAccess)
            {
                var logger = context.HttpContext.RequestServices.GetService<ILogger<UserAccessRequiredAttribute>>();
                if (logger != null)
                {
                    logger.LogWarning("User {Username} (ID: {UserId}) attempted to access account {AccountId} but was denied access", 
                        currentUser, user.Id, accountId.Value);
                }
                
                context.Result = new RedirectToActionResult("AccessDenied", "Auth", null);
                return;
            }
            else
            {
                var logger = context.HttpContext.RequestServices.GetService<ILogger<UserAccessRequiredAttribute>>();
                if (logger != null)
                {
                    logger.LogDebug("User {Username} (ID: {UserId}) granted access to account {AccountId}", 
                        currentUser, user.Id, accountId.Value);
                }
            }
            
            base.OnActionExecuting(context);
        }
        
        private int? GetAccountId(ActionExecutingContext context)
        {
            // Try to get account ID from route parameters (only look for accountId, not id)
            if (context.RouteData.Values.TryGetValue("accountId", out var accountIdObj))
            {
                if (accountIdObj != null && int.TryParse(accountIdObj.ToString(), out var accountId))
                {
                    return accountId;
                }
            }
            
            // Try to get account ID from query parameters (only look for accountId, not id)
            if (context.HttpContext.Request.Query.TryGetValue("accountId", out var accountIdQuery))
            {
                if (int.TryParse(accountIdQuery.FirstOrDefault(), out var accountId))
                {
                    return accountId;
                }
            }
            
            // Try to get account ID from form data (only look for accountId, not id)
            if (context.HttpContext.Request.HasFormContentType)
            {
                if (context.HttpContext.Request.Form.TryGetValue("accountId", out var accountIdForm))
                {
                    if (int.TryParse(accountIdForm.FirstOrDefault(), out var accountId))
                    {
                        return accountId;
                    }
                }
            }
            
            return null;
        }
    }
}

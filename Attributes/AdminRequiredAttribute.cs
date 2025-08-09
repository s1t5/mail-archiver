using MailArchiver.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MailArchiver.Attributes
{
    public class AdminRequiredAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var authService = context.HttpContext.RequestServices.GetService<IAuthenticationService>();
            
            // First check if user is authenticated
            if (authService == null || !authService.IsAuthenticated(context.HttpContext))
            {
                // Redirect to login page
                var returnUrl = context.HttpContext.Request.Path + context.HttpContext.Request.QueryString;
                context.Result = new RedirectToActionResult("Login", "Auth", new { returnUrl });
                return;
            }
            
            // Then check if user is admin
            var isAdmin = authService.IsCurrentUserAdmin(context.HttpContext);
            var username = authService.GetCurrentUser(context.HttpContext);
            
            if (!isAdmin)
            {
                var logger = context.HttpContext.RequestServices.GetService<ILogger<AdminRequiredAttribute>>();
                if (logger != null)
                {
                    logger.LogWarning("User {Username} attempted to access admin-only resource but was denied", username);
                }
                
                // User is authenticated but not admin - show access denied
                context.Result = new RedirectToActionResult("AccessDenied", "Auth", null);
                return;
            }
            else
            {
                var logger = context.HttpContext.RequestServices.GetService<ILogger<AdminRequiredAttribute>>();
                if (logger != null)
                {
                    logger.LogDebug("User {Username} is admin, granting access", username);
                }
            }

            base.OnActionExecuting(context);
        }
    }
}

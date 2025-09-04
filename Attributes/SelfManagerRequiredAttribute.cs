using MailArchiver.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MailArchiver.Attributes
{
    public class SelfManagerRequiredAttribute : ActionFilterAttribute
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
            
            // Then check if user is admin or self-manager
            var isAdmin = authService.IsCurrentUserAdmin(context.HttpContext);
            var isSelfManager = authService.IsCurrentUserSelfManager(context.HttpContext);
            var username = authService.GetCurrentUser(context.HttpContext);
            
            if (!isAdmin && !isSelfManager)
            {
                var logger = context.HttpContext.RequestServices.GetService<ILogger<SelfManagerRequiredAttribute>>();
                if (logger != null)
                {
                    logger.LogWarning("User {Username} attempted to access self-manager resource but was denied", username);
                }
                
                // User is authenticated but not admin or self-manager - show access denied
                context.Result = new RedirectToActionResult("AccessDenied", "Auth", null);
                return;
            }
            else
            {
                var logger = context.HttpContext.RequestServices.GetService<ILogger<SelfManagerRequiredAttribute>>();
                if (logger != null)
                {
                    logger.LogDebug("User {Username} is admin or self-manager, granting access", username);
                }
            }

            base.OnActionExecuting(context);
        }
    }
}

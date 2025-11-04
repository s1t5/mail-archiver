using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MailArchiver.Attributes
{
    /// <summary>
    /// Action filter that redirects users to the password change page if they must change their password.
    /// This is enforced when:
    /// 1. No mail accounts exist in the system (initial setup)
    /// 2. User is authenticated with default credentials from appsettings
    /// </summary>
    public class PasswordChangeRequiredAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var httpContext = context.HttpContext;
            
            // Check if user must change password (set during login)
            var mustChangePassword = httpContext.Session.GetString("MustChangePassword");
            
            if (mustChangePassword == "true")
            {
                // Allow access to ChangePassword, Logout, and Login actions
                var controller = context.RouteData.Values["controller"]?.ToString();
                var action = context.RouteData.Values["action"]?.ToString();
                
                if (controller == "Users" && action == "ChangePassword")
                {
                    // Allow access to change password page
                    return;
                }
                
                if (controller == "Auth" && (action == "Logout" || action == "Login"))
                {
                    // Allow logout
                    return;
                }
                
                // Redirect to change password page
                context.Result = new RedirectToActionResult("ChangePassword", "Users", null);
            }
            
            base.OnActionExecuting(context);
        }
    }
}

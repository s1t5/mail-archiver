using MailArchiver.Data;
using MailArchiver.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace MailArchiver.Attributes
{
    public class EmailAccessRequiredAttribute : ActionFilterAttribute
    {
        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
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
            
            // For admin users, allow access
            if (authService.IsCurrentUserAdmin(context.HttpContext))
            {
                await next();
                return;
            }
            
            // For regular users, check if they have access to the email's account
            var userService = context.HttpContext.RequestServices.GetService<IUserService>();
            var dbContext = context.HttpContext.RequestServices.GetService<MailArchiverDbContext>();
            
            if (userService == null || dbContext == null)
            {
                context.Result = new RedirectToActionResult("AccessDenied", "Auth", null);
                return;
            }
            
            // Get the email ID from the route parameters
            var emailId = GetEmailId(context);
            
            // Log the email ID we're checking
            var logger = context.HttpContext.RequestServices.GetService<ILogger<EmailAccessRequiredAttribute>>();
            if (logger != null)
            {
                logger.LogInformation("Checking access for email ID: {EmailId}", emailId);
            }
            
            // If no email ID is being accessed, allow general access
            // (the controller will handle filtering)
            if (!emailId.HasValue)
            {
                if (logger != null)
                {
                    logger.LogInformation("No email ID provided, allowing general access");
                }
                await next();
                return;
            }
            // Get the email and its associated account
            var email = await dbContext.ArchivedEmails
                .Where(e => e.Id == emailId.Value)
                .Select(e => new { e.Id, e.MailAccountId })
                .FirstOrDefaultAsync();
                
            if (email == null)
            {
                if (logger != null)
                {
                    logger.LogWarning("Email with ID {EmailId} not found", emailId.Value);
                }
                context.Result = new NotFoundResult();
                return;
            }
            
            if (logger != null)
            {
                logger.LogInformation("Email {EmailId} belongs to account {AccountId}", email.Id, email.MailAccountId);
            }
            // Check if the user has access to this email's account
            var username = authService.GetCurrentUser(context.HttpContext);
            var user = await userService.GetUserByUsernameAsync(username);
            
            if (user == null)
            {
                context.Result = new RedirectToActionResult("AccessDenied", "Auth", null);
                return;
            }
            
            var hasAccess = await userService.IsUserAuthorizedForAccountAsync(user.Id, email.MailAccountId);
            
            if (!hasAccess)
            {
                // Log for debugging purposes
                var accessLogger = context.HttpContext.RequestServices.GetService<ILogger<EmailAccessRequiredAttribute>>();
                if (accessLogger != null)
                {
                    accessLogger.LogWarning("User {Username} (ID: {UserId}) attempted to access email {EmailId} from account {AccountId} but was denied access", 
                        username, user.Id, email.Id, email.MailAccountId);
                }
                
                // Return NotFound instead of AccessDenied to avoid revealing existence
                context.Result = new NotFoundResult();
                return;
            }
            
            await next();
        }
        
        private int? GetEmailId(ActionExecutingContext context)
        {
            // Try to get email ID from route parameters (id or emailId)
            if (context.RouteData.Values.TryGetValue("id", out var emailIdObj) ||
                context.RouteData.Values.TryGetValue("emailId", out emailIdObj))
            {
                if (emailIdObj != null && int.TryParse(emailIdObj.ToString(), out var emailId))
                {
                    return emailId;
                }
            }
            
            // Try to get email ID from query parameters
            if (context.HttpContext.Request.Query.TryGetValue("id", out var emailIdQuery) ||
                context.HttpContext.Request.Query.TryGetValue("emailId", out emailIdQuery))
            {
                if (int.TryParse(emailIdQuery.FirstOrDefault(), out var emailId))
                {
                    return emailId;
                }
            }
            
            // Try to get email ID from form data
            if (context.HttpContext.Request.HasFormContentType)
            {
                if (context.HttpContext.Request.Form.TryGetValue("id", out var emailIdForm) ||
                    context.HttpContext.Request.Form.TryGetValue("emailId", out emailIdForm))
                {
                    if (int.TryParse(emailIdForm.FirstOrDefault(), out var emailId))
                    {
                        return emailId;
                    }
                }
            }
            
            return null;
        }
    }
}

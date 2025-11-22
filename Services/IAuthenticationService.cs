namespace MailArchiver.Services
{
    public interface IAuthenticationService
    {
        bool ValidateCredentials(string username, string password);
        void SignIn(HttpContext context, string username, bool rememberMe = false);
        void SignOut(HttpContext context);
        bool IsAuthenticated(HttpContext context);
        string GetCurrentUser(HttpContext context);
        bool IsCurrentUserAdmin(HttpContext context);
        bool IsCurrentUserSelfManager(HttpContext context);
    }
}

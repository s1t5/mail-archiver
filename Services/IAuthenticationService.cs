namespace MailArchiver.Services
{
    public interface IAuthenticationService
    {
        bool ValidateCredentials(string username, string password);
        Task StartUserSessionAsync(
            HttpContext context
            , string authenticationSchema
            , string username
            , bool rememberMe = false);
        void SignOut(HttpContext context);
        bool IsAuthenticated(HttpContext context);
        string GetCurrentUserDisplayName(HttpContext context);
        bool IsCurrentUserAdmin(HttpContext context);
        bool IsCurrentUserSelfManager(HttpContext context);
    }
}

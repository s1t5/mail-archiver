namespace MailArchiver.Services
{
    public interface IAuthenticationService
    {
        bool IsAuthenticationRequired();
        bool ValidateCredentials(string username, string password);
        void SignIn(HttpContext context, string username);
        void SignOut(HttpContext context);
        bool IsAuthenticated(HttpContext context);
        string GetCurrentUser(HttpContext context);
        bool IsCurrentUserAdmin(HttpContext context);
    }
}

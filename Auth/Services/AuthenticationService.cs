using MailArchiver.Services;

namespace MailArchiver.Auth.Services
{
    public class AuthenticationService : IAuthenticationService
    {
        public string GetCurrentUser(HttpContext context)
        {
            throw new NotImplementedException();
        }

        public bool IsAuthenticated(HttpContext context)
        {
            throw new NotImplementedException();
        }

        public bool IsAuthenticationRequired()
        {
            throw new NotImplementedException();
        }

        public bool IsCurrentUserAdmin(HttpContext context)
        {
            throw new NotImplementedException();
        }

        public bool IsCurrentUserSelfManager(HttpContext context)
        {
            throw new NotImplementedException();
        }

        public void SignIn(HttpContext context, string username, bool rememberMe = false)
        {
            throw new NotImplementedException();
        }

        public void SignOut(HttpContext context)
        {
            throw new NotImplementedException();
        }

        public bool ValidateCredentials(string username, string password)
        {
            throw new NotImplementedException();
        }
    }
}
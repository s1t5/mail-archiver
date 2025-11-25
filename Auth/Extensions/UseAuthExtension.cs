using MailArchiver.Auth.Middlewares;

namespace MailArchiver.Auth.Extensions
{
    public static class UseAuthExtension
    {
        public static WebApplication UseAuth(this WebApplication app)
        {
            // Add our custom authentication middleware
            app.UseMiddleware<AuthenticationMiddleware>();
            app.UseAuthorization();
            return app;
        }
    }
}

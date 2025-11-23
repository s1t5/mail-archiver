namespace MailArchiver.Auth.Exceptions
{
    public class UnknwonAuthenticationSchemeException : Exception
    {
        public string AuthenticationScheme { get; private set; }

        public UnknwonAuthenticationSchemeException(string authenticationScheme)
        {
            AuthenticationScheme = authenticationScheme;
        }
    }
}

namespace MailArchiver.Auth.Exceptions
{
    /// <summary>
    /// Raised when an OIDC sign-in cannot be completed because the response of the
    /// identity provider does not meet the requirements of the application.
    /// Carries the resource key of the message shown to the user, so the failure can
    /// be localized instead of exposing an exception message.
    /// </summary>
    public class OidcAuthenticationException : Exception
    {
        public string ResourceKey { get; }

        public object[] Args { get; }

        public OidcAuthenticationException(string resourceKey, params object[] args)
            : base(resourceKey)
        {
            ResourceKey = resourceKey;
            Args = args ?? Array.Empty<object>();
        }
    }
}

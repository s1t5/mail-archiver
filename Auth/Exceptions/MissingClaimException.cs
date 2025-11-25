namespace MailArchiver.Auth.Exceptions
{
    public class MissingClaimException : Exception
    {
        public string ClaimType { get; private set; }

        public MissingClaimException(string claimType)
        {
            ClaimType = claimType;
        }
    }
}

namespace MailArchiver.Models
{
    /// <summary>
    /// Configuration options for the MSA (personal Microsoft account) OAuth2 device code flow.
    /// When <see cref="DefaultClientId"/> is set, end-users do not need to create their own
    /// Azure App Registration — they only authorize via the device code flow.
    /// Self-hosters can override the default by entering a per-account ClientId in the UI.
    /// </summary>
    public class MsaOAuthOptions
    {
        public const string SectionName = "MsaOAuth";

        /// <summary>
        /// Shared ClientId registered by the mail-archiver maintainer. When non-empty,
        /// the Create/Edit forms hide the ClientId input and the device code flow uses
        /// this value automatically. Leave empty to require per-account ClientIds
        /// (the pre-default-ClientId behavior).
        /// </summary>
        public string DefaultClientId { get; set; } = string.Empty;

        /// <summary>
        /// OAuth2 authority endpoint. Defaults to the /consumers endpoint, matching the
        /// documented "Personal Microsoft accounts only" app registration this feature
        /// targets — the multi-tenant /common endpoint is rejected with AADSTS9002331 for
        /// such apps. Set https://login.microsoftonline.com/common/oauth2/v2.0 for a
        /// multi-tenant registration that must also serve organizational accounts.
        /// </summary>
        public string Authority { get; set; } = "https://login.microsoftonline.com/consumers/oauth2/v2.0";

        /// <summary>
        /// True when a shared default ClientId is configured and per-account ClientIds are optional.
        /// </summary>
        public bool HasDefaultClientId => !string.IsNullOrWhiteSpace(DefaultClientId);
    }
}

using System.Text.Json;

namespace MailArchiver.Auth.Helpers
{
    /// <summary>
    /// Reads the claims used for user provisioning out of an OIDC user info response.
    /// Providers differ in what they return, so every lookup walks a fallback chain and
    /// skips claims that are present but empty or not a string.
    /// </summary>
    internal static class OidcClaimExtractor
    {
        /// <summary>
        /// Name candidates in order of preference. <c>username</c> is not an OIDC
        /// standard claim but is returned by some providers such as the Synology SSO
        /// Server, so it is used as the last resort.
        /// </summary>
        internal static readonly string[] NameClaims =
        {
            "name",
            "preferred_username",
            "given_name",
            "username"
        };

        /// <summary>Email candidates in order of preference.</summary>
        internal static readonly string[] EmailClaims =
        {
            "email",
            "mail",
            "upn"
        };

        /// <summary>Subject candidates. <c>sub</c> is the only standard one.</summary>
        internal static readonly string[] SubjectClaims =
        {
            "sub"
        };

        public static string? ExtractName(JsonElement userInfo) => FirstNonEmpty(userInfo, NameClaims);

        public static string? ExtractEmail(JsonElement userInfo) => FirstNonEmpty(userInfo, EmailClaims);

        public static string? ExtractSubject(JsonElement userInfo) => FirstNonEmpty(userInfo, SubjectClaims);

        private static string? FirstNonEmpty(JsonElement userInfo, string[] candidates)
        {
            if (userInfo.ValueKind != JsonValueKind.Object)
                return null;

            foreach (var candidate in candidates)
            {
                if (!userInfo.TryGetProperty(candidate, out var element))
                    continue;

                // A claim of the wrong type must not abort the whole sign-in; treat it
                // as absent and let the next candidate answer instead.
                if (element.ValueKind != JsonValueKind.String)
                    continue;

                var value = element.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return null;
        }
    }
}

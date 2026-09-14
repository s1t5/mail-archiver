namespace MailArchiver.Models
{
    /// <summary>
    /// A mail folder as the exclusion rules need to see it: the full path and the folder's own name.
    ///
    /// Both, because <see cref="MailArchiver.Services.Shared.FolderExclusionMatcher"/> takes both:
    /// one rule anchors on the path separator, another matches the bare name, and the suffix rule on
    /// the name is deliberately unanchored. The name is carried as the provider reports it rather
    /// than cut from the path here, so no delimiter has to be guessed. That is a matter of not
    /// guessing, not of a known divergence: with these three rules a name cut at the last "/" is
    /// decided the same way, because wherever the two differ the full-path rules already answer.
    /// </summary>
    public class MailFolderInfo
    {
        public string FullName { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// True when the installation-wide exclusion list already covers this folder, so the account
        /// cannot sync it whatever its own list says.
        /// </summary>
        public bool GloballyExcluded { get; set; }
    }
}

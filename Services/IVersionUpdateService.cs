namespace MailArchiver.Services
{
    /// <summary>
    /// Result of a release notes check for the current version.
    /// </summary>
    public class ReleaseNotesResult
    {
        /// <summary>
        /// The version tag (e.g. "v2606.2") of the current application.
        /// </summary>
        public string? Version { get; set; }

        /// <summary>
        /// The release notes body in Markdown format from GitHub.
        /// </summary>
        public string? Body { get; set; }

        /// <summary>
        /// True when the current user has not yet seen the changelog for this version.
        /// </summary>
        public bool ShouldShow { get; set; }
    }

    /// <summary>
    /// Fetches GitHub release notes for the current application version and
    /// determines whether the current admin user has already seen them.
    /// </summary>
    public interface IVersionUpdateService
    {
        /// <summary>
        /// Retrieves the release notes for the currently-installed version from GitHub.
        /// Also checks if the given user has already dismissed these notes.
        /// </summary>
        /// <param name="userId">The ID of the current admin user.</param>
        /// <returns>A <see cref="ReleaseNotesResult"/> with the version, markdown body, and whether to show.</returns>
        Task<ReleaseNotesResult> GetReleaseNotesForCurrentVersionAsync(int userId);

        /// <summary>
        /// Marks the changelog for the currently-installed version as seen by the given user.
        /// </summary>
        /// <param name="userId">The ID of the admin user who dismissed the dialog.</param>
        Task DismissVersionAsync(int userId);
    }
}
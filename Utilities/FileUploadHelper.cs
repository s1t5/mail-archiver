namespace MailArchiver.Utilities
{
    /// <summary>
    /// Helper methods for validating uploaded files.
    /// </summary>
    public static class FileUploadHelper
    {
        private static readonly HashSet<string> AllowedImportExtensions =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ".mbox",
                ".eml",
                ".zip"
            };

        /// <summary>
        /// Checks whether the file name has an allowed extension for mail import operations.
        /// </summary>
        public static bool IsAllowedImportExtension(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            var extension = Path.GetExtension(fileName);
            return AllowedImportExtensions.Contains(extension);
        }
    }
}

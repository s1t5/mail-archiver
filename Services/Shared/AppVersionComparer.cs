namespace MailArchiver.Services.Shared
{
    /// <summary>
    /// Compares release version strings such as "2611.2" and "v2612.0".
    /// A plain string comparison would rank "2611.10" below "2611.9", and
    /// <see cref="Version.TryParse(string, out Version)"/> rejects the build metadata
    /// and prerelease suffixes that release tags may carry.
    /// </summary>
    public static class AppVersionComparer
    {
        /// <summary>
        /// Compares two version strings segment by segment.
        /// Returns false when either value is not a numeric version (for example "dev"),
        /// so callers can distinguish "older" from "not comparable".
        /// </summary>
        public static bool TryCompare(string? left, string? right, out int result)
        {
            result = 0;

            var leftParts = Parse(left);
            var rightParts = Parse(right);
            if (leftParts == null || rightParts == null)
                return false;

            var length = Math.Max(leftParts.Count, rightParts.Count);
            for (var i = 0; i < length; i++)
            {
                var leftPart = i < leftParts.Count ? leftParts[i] : 0;
                var rightPart = i < rightParts.Count ? rightParts[i] : 0;

                if (leftPart != rightPart)
                {
                    result = leftPart < rightPart ? -1 : 1;
                    return true;
                }
            }

            return true;
        }

        private static List<long>? Parse(string? version)
        {
            if (string.IsNullOrWhiteSpace(version))
                return null;

            var value = version.Trim();

            // Tags are published without a prefix, but tolerate "v2611.2".
            if (value.StartsWith('v') || value.StartsWith('V'))
                value = value[1..];

            // Cut build metadata (2611.2+abc123) and prerelease suffixes (2611.2-beta).
            var cut = value.IndexOfAny(['+', '-']);
            if (cut >= 0)
                value = value[..cut];

            if (value.Length == 0)
                return null;

            var parts = new List<long>();
            foreach (var segment in value.Split('.'))
            {
                var digits = 0;
                while (digits < segment.Length && char.IsAsciiDigit(segment[digits]))
                    digits++;

                // Every segment must start with a number; "dev" or a branch name
                // must not silently compare as 0.
                if (digits == 0 || !long.TryParse(segment[..digits], out var number))
                    return null;

                parts.Add(number);
            }

            return parts;
        }
    }
}

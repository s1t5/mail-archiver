using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Net.Http.Headers;

namespace MailArchiver.Services
{
    /// <summary>
    /// Maps Simplified Chinese browser language preferences (zh, zh-Hans, zh-CN, zh-SG)
    /// to the supported zh-CN culture so Accept-Language negotiation does not fall back to English.
    /// </summary>
    public class SimplifiedChineseRequestCultureProvider : RequestCultureProvider
    {
        private const string SimplifiedChineseCulture = "zh-CN";
        private readonly string[] _supportedCultures;

        public SimplifiedChineseRequestCultureProvider(IEnumerable<string> supportedCultures)
        {
            _supportedCultures = supportedCultures.ToArray();
        }

        public override Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
        {
            var languages = ParseAcceptLanguage(httpContext.Request.Headers.AcceptLanguage);
            if (languages.Count == 0)
            {
                return Task.FromResult<ProviderCultureResult?>(null);
            }

            foreach (var language in languages.OrderByDescending(language => language.Quality))
            {
                if (IsSimplifiedChinese(language.Name))
                {
                    return Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult(SimplifiedChineseCulture));
                }

                if (IsSupportedCulture(language.Name))
                {
                    return Task.FromResult<ProviderCultureResult?>(null);
                }
            }

            return Task.FromResult<ProviderCultureResult?>(null);
        }

        private static List<(string Name, double Quality)> ParseAcceptLanguage(Microsoft.Extensions.Primitives.StringValues acceptLanguage)
        {
            var languages = new List<(string Name, double Quality)>();
            foreach (var headerValue in acceptLanguage)
            {
                if (StringWithQualityHeaderValue.TryParseList(headerValue.Split(','), out var values))
                {
                    foreach (var value in values)
                    {
                        var name = value.Value.Value;
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            languages.Add((name, value.Quality ?? 1.0));
                        }
                    }
                }
            }

            return languages;
        }

        private static bool IsSimplifiedChinese(string cultureName)
        {
            return cultureName.Equals("zh", StringComparison.OrdinalIgnoreCase)
                || cultureName.Equals("zh-CN", StringComparison.OrdinalIgnoreCase)
                || cultureName.Equals("zh-SG", StringComparison.OrdinalIgnoreCase)
                || cultureName.Equals("zh-Hans", StringComparison.OrdinalIgnoreCase)
                || cultureName.StartsWith("zh-CN-", StringComparison.OrdinalIgnoreCase)
                || cultureName.StartsWith("zh-Hans-", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsSupportedCulture(string cultureName)
        {
            return _supportedCultures.Any(culture =>
                culture.Equals(cultureName, StringComparison.OrdinalIgnoreCase) ||
                cultureName.StartsWith(culture + "-", StringComparison.OrdinalIgnoreCase));
        }
    }
}

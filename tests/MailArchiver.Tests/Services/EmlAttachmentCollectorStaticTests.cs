using MailArchiver.Services.Providers.Eml;
using Xunit;

namespace MailArchiver.Tests.Services;

/// <summary>
/// Unit tests for <see cref="EmlAttachmentCollector.GetFileExtensionFromContentType"/>.
/// </summary>
public class EmlAttachmentCollectorStaticTests
{
    [Theory]
    [InlineData("image/jpeg", ".jpg")]
    [InlineData("image/jpg", ".jpg")]
    [InlineData("image/png", ".png")]
    [InlineData("image/gif", ".gif")]
    [InlineData("image/bmp", ".bmp")]
    [InlineData("image/webp", ".webp")]
    [InlineData("image/svg+xml", ".svg")]
    [InlineData("application/octet-stream", ".dat")]
    [InlineData("unknown/whatever", ".dat")]
    [InlineData(null, ".dat")]
    public void GetFileExtensionFromContentType_Mapping(string? contentType, string expected)
        => Assert.Equal(expected, EmlAttachmentCollector.GetFileExtensionFromContentType(contentType));
}
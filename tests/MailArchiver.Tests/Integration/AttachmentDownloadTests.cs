using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace MailArchiver.Tests.Integration;

[Collection("Integration")]
public class AttachmentDownloadTests : ApiTestBase
{
    public AttachmentDownloadTests(PostgresContainerFixture fixture) : base(fixture) { }

    private static readonly byte[] ExpectedBytes = Encoding.UTF8.GetBytes("%PDF-1.4 fake attachment bytes");

    [Fact]
    public async Task Download_ReturnsBytesAndContentType()
    {
        var response = await Client(Data.LimitedKey)
            .GetAsync($"/api/v1/emails/{Data.AttachmentEmailId}/attachments/{Data.AttachmentId}");

        response.EnsureSuccessStatusCode();
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(ExpectedBytes, bytes);
    }

    [Fact]
    public async Task Download_WrongEmailIdForAttachment_Returns404()
    {
        // Attachment exists but not under this email id.
        var response = await Client(Data.LimitedKey)
            .GetAsync($"/api/v1/emails/{Data.PlainBodyEmailId}/attachments/{Data.AttachmentId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Download_NonexistentAttachment_Returns404()
    {
        var response = await Client(Data.LimitedKey)
            .GetAsync($"/api/v1/emails/{Data.AttachmentEmailId}/attachments/999999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Download_WhenDisabled_Returns403()
    {
        using var disabled = new ApiWebApplicationFactory(Fixture.ConnectionString, allowAttachmentDownloads: false);
        var client = disabled.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Data.LimitedKey);

        var response = await client.GetAsync($"/api/v1/emails/{Data.AttachmentEmailId}/attachments/{Data.AttachmentId}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}

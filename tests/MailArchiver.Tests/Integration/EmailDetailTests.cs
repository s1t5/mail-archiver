using System.Net;
using System.Text.Json;

namespace MailArchiver.Tests.Integration;

[Collection("Integration")]
public class EmailDetailTests : ApiTestBase
{
    public EmailDetailTests(PostgresContainerFixture fixture) : base(fixture) { }

    private async Task<JsonElement> GetDetail(string key, int id)
    {
        var response = await Client(key).GetAsync($"/api/v1/emails/{id}");
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    [Fact]
    public async Task BodyFallback_PrefersOriginalBytes()
    {
        var e = await GetDetail(Data.LimitedKey, Data.OriginalBodyEmailId);
        Assert.Equal("<p>ORIGINAL HTML BODY</p>", e.GetProperty("htmlBody").GetString());
        Assert.Equal("ORIGINAL TEXT BODY", e.GetProperty("textBody").GetString());
    }

    [Fact]
    public async Task BodyFallback_FallsBackToUntruncated()
    {
        var e = await GetDetail(Data.LimitedKey, Data.UntruncatedBodyEmailId);
        Assert.Equal("<p>UNTRUNCATED HTML BODY</p>", e.GetProperty("htmlBody").GetString());
        Assert.Equal("UNTRUNCATED TEXT BODY", e.GetProperty("textBody").GetString());
    }

    [Fact]
    public async Task BodyFallback_FallsBackToRegular()
    {
        var e = await GetDetail(Data.LimitedKey, Data.PlainBodyEmailId);
        Assert.Equal("<p>PLAIN HTML BODY</p>", e.GetProperty("htmlBody").GetString());
        Assert.Equal("PLAIN TEXT BODY", e.GetProperty("textBody").GetString());
    }

    [Fact]
    public async Task Detail_IncludesAttachmentMetadata()
    {
        var e = await GetDetail(Data.LimitedKey, Data.AttachmentEmailId);
        var attachments = e.GetProperty("attachments");
        Assert.Equal(1, attachments.GetArrayLength());
        var att = attachments.EnumerateArray().First();
        Assert.Equal("report.pdf", att.GetProperty("fileName").GetString());
    }

    [Fact]
    public async Task CrossAccount_LimitedUser_Returns404()
    {
        var response = await Client(Data.LimitedKey).GetAsync($"/api/v1/emails/{Data.CrossAccountEmailId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CrossAccount_Admin_Succeeds()
    {
        var response = await Client(Data.AdminKey).GetAsync($"/api/v1/emails/{Data.CrossAccountEmailId}");
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task NonexistentEmail_Returns404()
    {
        var response = await Client(Data.AdminKey).GetAsync("/api/v1/emails/999999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

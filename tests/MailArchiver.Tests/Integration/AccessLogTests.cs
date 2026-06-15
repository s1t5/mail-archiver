using MailArchiver.Data;
using MailArchiver.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MailArchiver.Tests.Integration;

[Collection("Integration")]
public class AccessLogTests : ApiTestBase
{
    public AccessLogTests(PostgresContainerFixture fixture) : base(fixture) { }

    private const string LimitedUsername = "limited-int";

    private async Task<bool> HasLogAsync(AccessLogType type)
    {
        using var scope = NewScope();
        var db = scope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();
        return await db.AccessLogs.AnyAsync(l => l.Username == LimitedUsername && l.Type == type);
    }

    [Fact]
    public async Task Search_WritesSearchAccessLog()
    {
        var response = await Client(Data.LimitedKey).GetAsync("/api/v1/emails?q=Invoice");
        response.EnsureSuccessStatusCode();

        Assert.True(await HasLogAsync(AccessLogType.Search));
    }

    [Fact]
    public async Task OpenEmail_WritesOpenAccessLog()
    {
        var response = await Client(Data.LimitedKey).GetAsync($"/api/v1/emails/{Data.PlainBodyEmailId}");
        response.EnsureSuccessStatusCode();

        Assert.True(await HasLogAsync(AccessLogType.Open));
    }

    [Fact]
    public async Task DownloadAttachment_WritesDownloadAccessLog()
    {
        var response = await Client(Data.LimitedKey)
            .GetAsync($"/api/v1/emails/{Data.AttachmentEmailId}/attachments/{Data.AttachmentId}");
        response.EnsureSuccessStatusCode();

        Assert.True(await HasLogAsync(AccessLogType.Download));
    }
}

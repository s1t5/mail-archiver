using MailArchiver.Models;
using MailArchiver.Tests.Infrastructure;

namespace MailArchiver.Tests.Services;

[Collection(TestDbFixture.CollectionName)]
public class AuditExportDbSanityTests
{
    private readonly TestDbFixture _fixture;
    public AuditExportDbSanityTests(TestDbFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task AuditExportJobsTable_IsQueryable()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        try
        {
            var job = new AuditExportJob { Username = "sanity", FromDate = DateTime.UtcNow, ToDate = DateTime.UtcNow };
            scope.Context.AuditExportJobs.Add(job);
            await scope.Context.SaveChangesAsync();
            var rows = scope.Context.AuditExportJobs.Count(j => j.Username == "sanity");
            Assert.Equal(1, rows);
        }
        finally
        {
            await scope.RollbackAsync();
        }
    }
}

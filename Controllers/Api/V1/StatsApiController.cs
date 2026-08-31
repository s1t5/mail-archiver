using MailArchiver.Controllers.Api;
using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Models.Api;
using MailArchiver.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Linq;

namespace MailArchiver.Controllers.Api.V1;

[Route("api/v1/stats")]
public class StatsApiController : ApiControllerBase
{
    private readonly MailArchiverDbContext _context;
    private readonly ILogger<StatsApiController> _logger;

    public StatsApiController(MailArchiverDbContext context, ILogger<StatsApiController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<ActionResult<StatsDto>> GetStats()
    {
        var allowedAccountIds = await GetAllowedAccountIdsAsync();
        var isAdminScope = allowedAccountIds == null;

        IQueryable<ArchivedEmail> emailsQuery = _context.ArchivedEmails.AsQueryable();
        IQueryable<MailAccount> accountsQuery = _context.MailAccounts.AsQueryable();

        if (allowedAccountIds != null)
        {
            emailsQuery = emailsQuery.Where(e => allowedAccountIds.Contains(e.MailAccountId));
            accountsQuery = accountsQuery.Where(a => allowedAccountIds.Contains(a.Id));
        }

        var allowedEmailIdsQuery = emailsQuery.Select(e => e.Id);

        var attachmentsCount = isAdminScope
            ? await _context.EmailAttachments.CountAsync()
            : await _context.EmailAttachments
                .Where(a => allowedEmailIdsQuery.Contains(a.ArchivedEmailId))
                .CountAsync();

        // The instance-wide size is admin-only; restricted keys never see it (M5).
        var databaseSizeMb = isAdminScope
            ? await GetDatabaseSizeInMBAsync(allowedEmailIdsQuery, isAdminScope: true)
            : 0;

        return Ok(StatsComposer.Compose(
            await emailsQuery.CountAsync(),
            await accountsQuery.CountAsync(),
            attachmentsCount,
            databaseSizeMb,
            isAdminScope));
    }

    private async Task<long> GetDatabaseSizeInMBAsync(IQueryable<int> allowedEmailIdsQuery, bool isAdminScope)
    {
        try
        {
            using var connection = new NpgsqlConnection(_context.Database.GetConnectionString());
            await connection.OpenAsync();

            var sql = "SELECT pg_database_size(current_database())";
            using var command = new NpgsqlCommand(sql, connection);
            var result = await command.ExecuteScalarAsync();

            var bytes = Convert.ToInt64(result);
            return bytes / (1024L * 1024L);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting database size: {Message}", ex.Message);
            // Scoped fallback: only the attachments the caller may see (M5).
            var scopedQuery = isAdminScope
                ? _context.EmailAttachments
                : _context.EmailAttachments.Where(a => allowedEmailIdsQuery.Contains(a.ArchivedEmailId));
            return (await scopedQuery.SumAsync(a => (long)a.Size)) / (1024L * 1024L);
        }
    }
}
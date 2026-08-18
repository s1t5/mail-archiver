using MailArchiver.Controllers.Api;
using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Models.Api;
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

        IQueryable<ArchivedEmail> emailsQuery = _context.ArchivedEmails.AsQueryable();
        IQueryable<MailAccount> accountsQuery = _context.MailAccounts.AsQueryable();

        if (allowedAccountIds != null)
        {
            emailsQuery = emailsQuery.Where(e => allowedAccountIds.Contains(e.MailAccountId));
            accountsQuery = accountsQuery.Where(a => allowedAccountIds.Contains(a.Id));
        }

        var allowedEmailIdsQuery = emailsQuery.Select(e => e.Id);

        var attachmentsCount = allowedAccountIds == null
            ? await _context.EmailAttachments.CountAsync()
            : await _context.EmailAttachments
                .Where(a => allowedEmailIdsQuery.Contains(a.ArchivedEmailId))
                .CountAsync();

        var dto = new StatsDto
        {
            Emails = await emailsQuery.CountAsync(),
            Accounts = await accountsQuery.CountAsync(),
            Attachments = attachmentsCount,
            DatabaseSizeInMB = (await GetDatabaseSizeInMBAsync()).ToString("0")
        };

        return Ok(dto);
    }

    private async Task<long> GetDatabaseSizeInMBAsync()
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
            return (await _context.EmailAttachments.SumAsync(a => (long)a.Size)) / (1024L * 1024L);
        }
    }
}
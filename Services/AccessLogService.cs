using MailArchiver.Data;
using MailArchiver.Models;
using Microsoft.EntityFrameworkCore;

namespace MailArchiver.Services
{
    public interface IAccessLogService
    {
        Task LogAccessAsync(string username, AccessLogType type, int? emailId = null, string emailSubject = null, string emailFrom = null, string searchParameters = null, int? mailAccountId = null);
        Task<List<AccessLog>> GetLogsForUserAsync(string username, int limit = 100);
        Task<List<AccessLog>> GetLogsForAdminAsync(int limit = 1000);
        Task<List<AccessLog>> GetLogsForUserAsync(string username, DateTime? fromDate = null, DateTime? toDate = null);
        Task<List<AccessLog>> GetLogsForAdminAsync(DateTime? fromDate = null, DateTime? toDate = null);
    }

    public class AccessLogService : IAccessLogService
    {
        private readonly MailArchiverDbContext _context;

        public AccessLogService(MailArchiverDbContext context)
        {
            _context = context;
        }

        public async Task LogAccessAsync(string username, AccessLogType type, int? emailId = null, string emailSubject = null, string emailFrom = null, string searchParameters = null, int? mailAccountId = null)
        {
            var logEntry = new AccessLog
            {
                Username = username,
                Type = type,
                Timestamp = DateTime.UtcNow,
                EmailId = emailId,
                EmailSubject = emailSubject,
                EmailFrom = emailFrom,
                SearchParameters = searchParameters,
                MailAccountId = mailAccountId
            };

            _context.AccessLogs.Add(logEntry);
            await _context.SaveChangesAsync();
        }

        public async Task<List<AccessLog>> GetLogsForUserAsync(string username, int limit = 100)
        {
            return await _context.AccessLogs
                .Where(log => log.Username == username)
                .OrderByDescending(log => log.Timestamp)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<List<AccessLog>> GetLogsForAdminAsync(int limit = 1000)
        {
            return await _context.AccessLogs
                .OrderByDescending(log => log.Timestamp)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<List<AccessLog>> GetLogsForUserAsync(string username, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var query = _context.AccessLogs.Where(log => log.Username == username);

            if (fromDate.HasValue)
            {
                query = query.Where(log => log.Timestamp >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                var toDateEndOfDay = toDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(log => log.Timestamp <= toDateEndOfDay);
            }

            return await query
                .OrderByDescending(log => log.Timestamp)
                .ToListAsync();
        }

        public async Task<List<AccessLog>> GetLogsForAdminAsync(DateTime? fromDate = null, DateTime? toDate = null)
        {
            var query = _context.AccessLogs.AsQueryable();

            if (fromDate.HasValue)
            {
                query = query.Where(log => log.Timestamp >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                var toDateEndOfDay = toDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(log => log.Timestamp <= toDateEndOfDay);
            }

            return await query
                .OrderByDescending(log => log.Timestamp)
                .ToListAsync();
        }
    }
}

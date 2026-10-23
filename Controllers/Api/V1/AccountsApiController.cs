using MailArchiver.Controllers.Api;
using MailArchiver.Data;
using MailArchiver.Models.Api;
using MailArchiver.Services.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MailArchiver.Controllers.Api.V1;

[Route("api/v1/accounts")]
public class AccountsApiController : ApiControllerBase
{
    private readonly MailArchiverDbContext _context;
    private readonly EmailCoreService _emailCoreService;

    public AccountsApiController(MailArchiverDbContext context, EmailCoreService emailCoreService)
    {
        _context = context;
        _emailCoreService = emailCoreService;
    }

    [HttpGet("")]
    public async Task<ActionResult<List<MailAccountDto>>> GetAccounts()
    {
        var allowedAccountIds = await GetAllowedAccountIdsAsync();

        var accountsQuery = _context.MailAccounts.AsQueryable();
        if (allowedAccountIds != null)
        {
            accountsQuery = accountsQuery.Where(a => allowedAccountIds.Contains(a.Id));
        }

        var accounts = await accountsQuery
            .OrderBy(a => a.Name)
            .ToListAsync();

        return Ok(accounts.Select(MailAccountDto.FromEntity).ToList());
    }

    [HttpGet("{id:int}/folders")]
    public async Task<ActionResult<List<FolderNodeDto>>> GetFolders(int id)
    {
        var allowedAccountIds = await GetAllowedAccountIdsAsync();
        if (allowedAccountIds != null && !allowedAccountIds.Contains(id))
        {
            return NotFound();
        }

        var accountExists = await _context.MailAccounts.AnyAsync(a => a.Id == id);
        if (!accountExists)
        {
            return NotFound();
        }

        var folders = await _emailCoreService.GetFolderTreeAsync(id, allowedAccountIds);
        return Ok(folders.Select(FolderNodeDto.FromNode).ToList());
    }
}

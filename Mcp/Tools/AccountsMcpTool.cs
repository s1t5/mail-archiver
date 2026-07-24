using System.ComponentModel;
using MailArchiver.Data;
using MailArchiver.Models.Api;
using MailArchiver.Services.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;

namespace MailArchiver.Mcp.Tools;

/// <summary>
/// MCP tools for listing mail accounts and their folder trees.
/// Mirrors the <c>api/v1/accounts</c> REST endpoints. Access is scoped by the
/// same API-key claim logic as the REST API (see McpToolBase.GetAllowedAccountIdsAsync).
/// </summary>
[McpServerToolType]
public class AccountsMcpTool : McpToolBase
{
    private readonly MailArchiverDbContext _context;
    private readonly EmailCoreService _emailCoreService;

    public AccountsMcpTool(
        MailArchiverDbContext context,
        EmailCoreService emailCoreService,
        IHttpContextAccessor httpContextAccessor)
        : base(httpContextAccessor)
    {
        _context = context;
        _emailCoreService = emailCoreService;
    }

    [McpServerTool(Name = "list_accounts")]
    [Description("Lists the mail accounts the current API key is allowed to access. Returns id, name, email address, provider, enabled flag and last sync time.")]
    public async Task<List<MailAccountDto>> ListAccountsAsync()
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

        return accounts.Select(MailAccountDto.FromEntity).ToList();
    }

    [McpServerTool(Name = "list_folders")]
    [Description("Lists the folder tree of a mail account. Returns nested folders with name, full path, total email count and hierarchy level.")]
    public async Task<List<FolderNodeDto>> ListFoldersAsync(
        [Description("The mail account id to list folders for. Use list_accounts to find valid ids.")] int accountId)
    {
        var allowedAccountIds = await GetAllowedAccountIdsAsync();
        if (allowedAccountIds != null && !allowedAccountIds.Contains(accountId))
        {
            return new List<FolderNodeDto>();
        }

        var accountExists = await _context.MailAccounts.AnyAsync(a => a.Id == accountId);
        if (!accountExists)
        {
            return new List<FolderNodeDto>();
        }

        var folders = await _emailCoreService.GetFolderTreeAsync(accountId, allowedAccountIds);
        return folders.Select(FolderNodeDto.FromNode).ToList();
    }
}
using MailArchiver.Controllers.Api;
using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Models.Api;
using MailArchiver.Services;
using MailArchiver.Services.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MailArchiver.Controllers.Api.V1;

[Route("api/v1/accounts")]
public class AccountsApiController : ApiControllerBase
{
    private readonly MailArchiverDbContext _context;
    private readonly EmailCoreService _emailCoreService;
    private readonly IMailAccountDeletionService _accountDeletionService;
    private readonly IAccessLogService _accessLogService;
    private readonly MsaOAuthOptions _msaOptions;

    public AccountsApiController(
        MailArchiverDbContext context,
        EmailCoreService emailCoreService,
        IMailAccountDeletionService accountDeletionService,
        IAccessLogService accessLogService,
        IOptions<MsaOAuthOptions> msaOptions)
    {
        _context = context;
        _emailCoreService = emailCoreService;
        _accountDeletionService = accountDeletionService;
        _accessLogService = accessLogService;
        _msaOptions = msaOptions.Value;
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

    [HttpGet("{id:int}")]
    public async Task<ActionResult<MailAccountDetailDto>> GetAccount(int id)
    {
        var (account, error) = await FindVisibleAccountAsync(id);
        if (error != null)
        {
            return error;
        }

        return Ok(MailAccountDetailDto.FromEntity(account!));
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

    [HttpPost("")]
    public async Task<ActionResult<MailAccountDetailDto>> CreateAccount([FromBody] CreateMailAccountRequest request)
    {
        if (RequireWriteAccess() is { } writeError) return writeError;
        if (RequireAccountManager() is { } roleError) return roleError;

        var errors = MailAccountRequestValidator.ValidateCreate(request, _msaOptions.HasDefaultClientId);
        if (errors.Count > 0)
        {
            return ValidationProblem(new ValidationProblemDetails(errors));
        }

        var emailAddress = request.EmailAddress!.Trim();
        var duplicate = await _context.MailAccounts
            .AnyAsync(a => a.EmailAddress.ToLower() == emailAddress.ToLower());
        if (duplicate)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Account already exists.",
                detail: $"A mail account for '{emailAddress}' already exists.");
        }

        var account = MailAccountRequestValidator.ToEntity(request);

        _context.MailAccounts.Add(account);
        await _context.SaveChangesAsync();

        // Mirror the web UI: a self-manager automatically gets access to the
        // account they just created, otherwise the new account would be
        // invisible to them. Administrators see every account anyway.
        if (!IsAdmin && CurrentUserId is int userId)
        {
            _context.UserMailAccounts.Add(new UserMailAccount
            {
                UserId = userId,
                MailAccountId = account.Id
            });
            await _context.SaveChangesAsync();
        }

        await _accessLogService.LogAccessAsync(
            CurrentUsername,
            AccessLogType.Account,
            searchParameters: Truncate($"Created mail account via API: {account.Name}", 255),
            mailAccountId: account.Id);

        return CreatedAtAction(nameof(GetAccount), new { id = account.Id }, MailAccountDetailDto.FromEntity(account));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<MailAccountDetailDto>> UpdateAccount(int id, [FromBody] UpdateMailAccountRequest request)
    {
        if (RequireWriteAccess() is { } writeError) return writeError;
        if (RequireAccountManager() is { } roleError) return roleError;

        var (account, error) = await FindVisibleAccountAsync(id);
        if (error != null)
        {
            return error;
        }

        var errors = MailAccountRequestValidator.ValidateUpdate(account!, request);
        if (errors.Count > 0)
        {
            return ValidationProblem(new ValidationProblemDetails(errors));
        }

        MailAccountRequestValidator.Apply(request, account!);
        await _context.SaveChangesAsync();

        await _accessLogService.LogAccessAsync(
            CurrentUsername,
            AccessLogType.Account,
            searchParameters: Truncate($"Updated mail account via API: {account!.Name}", 255),
            mailAccountId: account.Id);

        return Ok(MailAccountDetailDto.FromEntity(account));
    }

    /// <summary>
    /// Partial update: only the properties supplied with a non-null value are
    /// changed. Use PUT to clear a nullable setting (e.g. a retention window).
    /// Secrets are write-only — omit them to keep the stored value, send
    /// <c>""</c> to clear it.
    /// </summary>
    [HttpPatch("{id:int}")]
    public async Task<ActionResult<MailAccountDetailDto>> PatchAccount(int id, [FromBody] UpdateMailAccountRequest request)
    {
        if (RequireWriteAccess() is { } writeError) return writeError;
        if (RequireAccountManager() is { } roleError) return roleError;

        var (account, error) = await FindVisibleAccountAsync(id);
        if (error != null)
        {
            return error;
        }

        // Merge the patch onto the current state first: omitted (null) properties
        // keep their value, then the merged result is validated as a whole.
        var merged = MailAccountRequestValidator.Merge(account!, request);

        var errors = MailAccountRequestValidator.ValidateUpdate(account!, merged);
        if (errors.Count > 0)
        {
            return ValidationProblem(new ValidationProblemDetails(errors));
        }

        MailAccountRequestValidator.Apply(merged, account!);
        await _context.SaveChangesAsync();

        await _accessLogService.LogAccessAsync(
            CurrentUsername,
            AccessLogType.Account,
            searchParameters: Truncate($"Patched mail account via API: {account!.Name}", 255),
            mailAccountId: account.Id);

        return Ok(MailAccountDetailDto.FromEntity(account));
    }

    /// <summary>
    /// Deletes an account and every message archived for it. The deletion runs
    /// asynchronously; poll <c>GET /api/v1/accounts/deletions/{jobId}</c> for
    /// progress.
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<ActionResult<AccountDeletionJobDto>> DeleteAccount(int id)
    {
        if (RequireWriteAccess() is { } writeError) return writeError;
        if (RequireAccountManager() is { } roleError) return roleError;
        if (RequireDeletionAllowed() is { } policyError) return policyError;

        var options = HttpContext.RequestServices.GetRequiredService<IOptions<ApiOptions>>().Value;
        if (!options.AllowAccountDeletion)
        {
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Account deletion is disabled.",
                detail: "Deleting mail accounts through the API is disabled. Set Api:AllowAccountDeletion=true to enable it.");
        }

        var (account, error) = await FindVisibleAccountAsync(id);
        if (error != null)
        {
            return error;
        }

        // Reject a second job for the same account instead of queueing parallel
        // deletions of the same data.
        var activeJob = _accountDeletionService.GetAllJobs()
            .FirstOrDefault(j => j.MailAccountId == id && !j.IsCompleted);
        if (activeJob != null)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Deletion already queued.",
                detail: $"A deletion job for this account is already active (jobId {activeJob.JobId}).",
                extensions: new Dictionary<string, object?>
                {
                    ["jobId"] = activeJob.JobId
                });
        }

        var jobId = _accountDeletionService.QueueDeletion(id, account!.Name, CurrentUsername);
        var job = _accountDeletionService.GetJob(jobId)!;

        await _accessLogService.LogAccessAsync(
            CurrentUsername,
            AccessLogType.Account,
            searchParameters: Truncate($"Queued async deletion via API for mail account: {account.Name}", 255),
            mailAccountId: account.Id);

        return AcceptedAtAction(nameof(GetDeletionJob), new { jobId }, AccountDeletionJobDto.FromJob(job));
    }

    /// <summary>Status of an account deletion started through the API.</summary>
    [HttpGet("deletions/{jobId}")]
    public async Task<ActionResult<AccountDeletionJobDto>> GetDeletionJob(string jobId)
    {
        var job = _accountDeletionService.GetJob(jobId);
        if (job == null)
        {
            return NotFound();
        }

        var allowed = await GetAllowedAccountIdsAsync();
        if (allowed != null && !allowed.Contains(job.MailAccountId))
        {
            return NotFound();
        }

        return Ok(AccountDeletionJobDto.FromJob(job));
    }

    // ---- helpers ---------------------------------------------------------------

    private async Task<(MailAccount? Account, ActionResult? Error)> FindVisibleAccountAsync(int id)
    {
        var allowed = await GetAllowedAccountIdsAsync();
        if (allowed != null && !allowed.Contains(id))
        {
            return (null, NotFound());
        }

        var account = await _context.MailAccounts.FirstOrDefaultAsync(a => a.Id == id);
        if (account == null)
        {
            return (null, NotFound());
        }

        return (account, null);
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}

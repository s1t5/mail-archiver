using MailArchiver.Controllers.Api;
using MailArchiver.Models;
using MailArchiver.Models.Api;
using MailArchiver.Services;
using Microsoft.AspNetCore.Mvc;

namespace MailArchiver.Controllers.Api.V1;

/// <summary>
/// Self-service management of the caller's own API keys. A key can never create
/// keys for another user, and creating a key requires a write-scoped key — a
/// read-only key cannot escalate itself.
/// </summary>
[Route("api/v1/keys")]
public class ApiKeysApiController : ApiControllerBase
{
    private const int MaxNameLength = 100;

    private readonly IApiKeyService _apiKeyService;
    private readonly IAccessLogService _accessLogService;

    public ApiKeysApiController(IApiKeyService apiKeyService, IAccessLogService accessLogService)
    {
        _apiKeyService = apiKeyService;
        _accessLogService = accessLogService;
    }

    [HttpGet("")]
    public async Task<ActionResult<List<ApiKeyDto>>> GetKeys()
    {
        if (!HasScope(ApiKeyScopes.Read))
        {
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Insufficient scope.",
                detail: "Listing API keys requires the 'read' scope.");
        }

        if (CurrentUserId is not int userId)
        {
            return Unauthorized();
        }

        var keys = await _apiKeyService.GetKeysForUserAsync(userId);
        return Ok(keys.Select(k => ApiKeyDto.FromEntity(k)).ToList());
    }

    [HttpPost("")]
    public async Task<ActionResult<CreatedApiKeyDto>> CreateKey([FromBody] CreateApiKeyRequest request)
    {
        if (RequireWriteAccess() is { } writeError) return writeError;

        if (CurrentUserId is not int userId)
        {
            return Unauthorized();
        }

        var errors = new Dictionary<string, string[]>();

        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            errors["name"] = new[] { "A name is required." };
        }
        else if (name.Length > MaxNameLength)
        {
            errors["name"] = new[] { $"The name must not exceed {MaxNameLength} characters." };
        }

        DateTime? expiresAt = request.ExpiresAt;
        if (expiresAt.HasValue)
        {
            if (expiresAt.Value.Kind == DateTimeKind.Unspecified)
            {
                expiresAt = DateTime.SpecifyKind(expiresAt.Value, DateTimeKind.Utc);
            }

            if (expiresAt.Value <= DateTime.UtcNow)
            {
                errors["expiresAt"] = new[] { "expiresAt must be in the future." };
            }
        }

        var requestedScopes = request.Scopes ?? new List<string> { ApiKeyScopes.Read };
        if (!ApiKeyScopes.TryNormalize(requestedScopes, out var normalizedScopes, out var scopeError))
        {
            errors["scopes"] = new[] { scopeError! };
        }

        if (errors.Count > 0)
        {
            return ValidationProblem(new ValidationProblemDetails(errors));
        }

        var (entity, plaintextKey) = await _apiKeyService.CreateAsync(
            userId, name!, expiresAt, requestedScopes);

        await _accessLogService.LogAccessAsync(
            CurrentUsername,
            AccessLogType.Account,
            searchParameters: Truncate($"Created API key '{entity.Name}' via API (scopes: {entity.Scopes})", 255));

        var response = new CreatedApiKeyDto
        {
            Id = entity.Id,
            Name = entity.Name,
            KeyPrefix = entity.KeyPrefix,
            Scopes = ApiKeyScopes.Parse(entity.Scopes).ToList(),
            CreatedAt = entity.CreatedAt,
            LastUsedAt = entity.LastUsedAt,
            ExpiresAt = entity.ExpiresAt,
            RevokedAt = entity.RevokedAt,
            IsActive = entity.IsActive,
            Key = plaintextKey
        };

        return Created("/api/v1/keys", response);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> RevokeKey(int id)
    {
        if (RequireWriteAccess() is { } writeError) return writeError;

        if (CurrentUserId is not int userId)
        {
            return Unauthorized();
        }

        var revoked = await _apiKeyService.RevokeAsync(id, userId, IsAdmin);
        if (!revoked)
        {
            return NotFound();
        }

        await _accessLogService.LogAccessAsync(
            CurrentUsername,
            AccessLogType.Account,
            searchParameters: Truncate($"Revoked API key #{id} via API", 255));

        return NoContent();
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}

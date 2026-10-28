using System.Text.RegularExpressions;

namespace MailArchiver.Models.Api;

/// <summary>
/// Body of <c>POST /api/v1/accounts</c>. Only the fields that are required for
/// the selected provider have to be supplied; everything else falls back to the
/// same defaults the web UI uses.
/// </summary>
public class CreateMailAccountRequest
{
    public string? Name { get; set; }
    public string? EmailAddress { get; set; }

    /// <summary>IMAP (default), M365 or MSA. IMPORT accounts cannot be created via the API.</summary>
    public string? Provider { get; set; }

    public bool? IsEnabled { get; set; }

    public string? ImapServer { get; set; }
    public int? ImapPort { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool? UseSsl { get; set; }

    public List<string>? ExcludedFolders { get; set; }
    public int? DeleteAfterDays { get; set; }
    public int? LocalRetentionDays { get; set; }
    public int? SyncIntervalMinutes { get; set; }
    public int? FullSyncIntervalHours { get; set; }

    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? TenantId { get; set; }
}

/// <summary>
/// Body of <c>PUT /api/v1/accounts/{id}</c>. The provider and the e-mail address
/// of an account are immutable; everything else is replaced by this request.
/// Omitted nullable settings are cleared. Secrets are write-only: a null value
/// keeps the stored secret, an empty string clears it.
/// </summary>
public class UpdateMailAccountRequest
{
    public string? Name { get; set; }
    public bool? IsEnabled { get; set; }

    public string? ImapServer { get; set; }
    public int? ImapPort { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool? UseSsl { get; set; }

    public List<string>? ExcludedFolders { get; set; }
    public int? DeleteAfterDays { get; set; }
    public int? LocalRetentionDays { get; set; }
    public int? SyncIntervalMinutes { get; set; }
    public int? FullSyncIntervalHours { get; set; }

    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? TenantId { get; set; }
}

/// <summary>
/// Validation and mapping shared by the create and update endpoints. Pure (no
/// database access) so the rules are unit-testable; uniqueness of the address is
/// checked by the controller.
/// </summary>
public static class MailAccountRequestValidator
{
    private const int MaxNameLength = 200;
    private const int MaxAddressLength = 320;
    private const int MaxHostLength = 255;
    private const int MaxSecretLength = 1000;
    private const int MaxExcludedFolders = 200;
    private const int MaxFolderLength = 255;

    private static readonly Regex AddressPattern =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static Dictionary<string, string[]> ValidateCreate(
        CreateMailAccountRequest request, bool msaHasDefaultClientId)
    {
        var errors = new Dictionary<string, string[]>();

        ValidateName(request.Name, errors);
        ValidateAddress(request.EmailAddress, errors);

        var provider = ParseProvider(request.Provider, errors);

        if (provider == ProviderType.IMAP)
        {
            ValidateImap(request.ImapServer, request.ImapPort, request.Username, errors);
            if (string.IsNullOrWhiteSpace(request.Password))
            {
                Add(errors, "password", "A password is required for IMAP accounts.");
            }
            else if (request.Password.Length > MaxSecretLength)
            {
                Add(errors, "password", $"The password must not exceed {MaxSecretLength} characters.");
            }
        }
        else if (provider == ProviderType.M365)
        {
            RequireSecret(request.ClientId, "clientId", "A clientId is required for M365 accounts.", errors);
            RequireSecret(request.ClientSecret, "clientSecret", "A clientSecret is required for M365 accounts.", errors);
            RequireSecret(request.TenantId, "tenantId", "A tenantId is required for M365 accounts.", errors);
        }
        else if (provider == ProviderType.MSA)
        {
            if (!msaHasDefaultClientId)
            {
                RequireSecret(request.ClientId, "clientId",
                    "A clientId is required for MSA accounts unless a default client id is configured.", errors);
            }
            else if (request.ClientId is { Length: > MaxSecretLength })
            {
                Add(errors, "clientId", $"The clientId must not exceed {MaxSecretLength} characters.");
            }
        }

        ValidateRetention(request.DeleteAfterDays, request.LocalRetentionDays, errors);
        ValidateSync(request.SyncIntervalMinutes, request.FullSyncIntervalHours, errors);
        ValidateExcludedFolders(request.ExcludedFolders, errors);

        return errors;
    }

    public static Dictionary<string, string[]> ValidateUpdate(
        MailAccount account, UpdateMailAccountRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        ValidateName(request.Name, errors);

        if (account.Provider == ProviderType.IMAP)
        {
            ValidateImap(request.ImapServer, request.ImapPort, request.Username, errors);
            if (request.Password is { Length: > MaxSecretLength })
            {
                Add(errors, "password", $"The password must not exceed {MaxSecretLength} characters.");
            }
        }
        else if (account.Provider == ProviderType.M365)
        {
            RequireSecret(request.ClientId, "clientId", "A clientId is required for M365 accounts.", errors);
            RequireSecret(request.TenantId, "tenantId", "A tenantId is required for M365 accounts.", errors);
            if (request.ClientSecret is { Length: > MaxSecretLength })
            {
                Add(errors, "clientSecret", $"The clientSecret must not exceed {MaxSecretLength} characters.");
            }
        }
        else if (account.Provider == ProviderType.MSA)
        {
            if (request.ClientId is { Length: > MaxSecretLength })
            {
                Add(errors, "clientId", $"The clientId must not exceed {MaxSecretLength} characters.");
            }
        }

        ValidateRetention(request.DeleteAfterDays, request.LocalRetentionDays, errors);
        ValidateSync(request.SyncIntervalMinutes, request.FullSyncIntervalHours, errors);
        ValidateExcludedFolders(request.ExcludedFolders, errors);

        return errors;
    }

    /// <summary>Maps a validated create request onto a new <see cref="MailAccount"/>.</summary>
    public static MailAccount ToEntity(CreateMailAccountRequest request)
    {
        var provider = ParseProvider(request.Provider, new Dictionary<string, string[]>());

        var account = new MailAccount
        {
            Name = request.Name!.Trim(),
            EmailAddress = request.EmailAddress!.Trim(),
            Provider = provider,
            IsEnabled = request.IsEnabled ?? true,
            LastSync = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ExcludedFolders = JoinFolders(request.ExcludedFolders),
            DeleteAfterDays = request.DeleteAfterDays,
            LocalRetentionDays = request.LocalRetentionDays,
            SyncIntervalMinutes = request.SyncIntervalMinutes,
            FullSyncIntervalHours = request.FullSyncIntervalHours
        };

        switch (provider)
        {
            case ProviderType.IMAP:
                account.ImapServer = request.ImapServer!.Trim();
                account.ImapPort = request.ImapPort ?? 993;
                account.Username = request.Username!.Trim();
                account.Password = request.Password;
                account.UseSSL = request.UseSsl ?? true;
                break;

            case ProviderType.M365:
                account.ClientId = request.ClientId!.Trim();
                account.ClientSecret = request.ClientSecret;
                account.TenantId = request.TenantId!.Trim();
                account.UseSSL = true;
                break;

            case ProviderType.MSA:
                account.ImapServer = "outlook.office365.com";
                account.ImapPort = 993;
                account.ClientId = string.IsNullOrWhiteSpace(request.ClientId) ? null : request.ClientId.Trim();
                account.ClientSecret = string.IsNullOrWhiteSpace(request.ClientSecret) ? null : request.ClientSecret;
                account.UseSSL = true;
                break;
        }

        return account;
    }

    /// <summary>
    /// Applies a validated PUT request to an existing account. Null secrets keep
    /// the stored value; an empty string clears it.
    /// </summary>
    public static void Apply(UpdateMailAccountRequest request, MailAccount account)
    {
        account.Name = request.Name!.Trim();
        account.IsEnabled = request.IsEnabled ?? account.IsEnabled;
        account.ExcludedFolders = JoinFolders(request.ExcludedFolders);
        account.DeleteAfterDays = request.DeleteAfterDays;
        account.LocalRetentionDays = request.LocalRetentionDays;
        account.SyncIntervalMinutes = request.SyncIntervalMinutes;
        account.FullSyncIntervalHours = request.FullSyncIntervalHours;

        if (account.Provider == ProviderType.IMAP)
        {
            account.ImapServer = request.ImapServer!.Trim();
            account.ImapPort = request.ImapPort ?? 993;
            account.Username = request.Username!.Trim();
            account.UseSSL = request.UseSsl ?? account.UseSSL;
            if (request.Password != null)
            {
                account.Password = request.Password.Length == 0 ? null : request.Password;
            }
        }
        else if (account.Provider == ProviderType.M365)
        {
            account.ClientId = request.ClientId!.Trim();
            account.TenantId = request.TenantId!.Trim();
            if (request.ClientSecret != null)
            {
                account.ClientSecret = request.ClientSecret.Length == 0 ? null : request.ClientSecret;
            }
        }
        else if (account.Provider == ProviderType.MSA)
        {
            if (request.ClientId != null)
            {
                account.ClientId = request.ClientId.Length == 0 ? null : request.ClientId.Trim();
            }
            if (request.ClientSecret != null)
            {
                account.ClientSecret = request.ClientSecret.Length == 0 ? null : request.ClientSecret;
            }
        }
    }

    // ---- helpers ---------------------------------------------------------------

    private static ProviderType ParseProvider(string? value, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ProviderType.IMAP;
        }

        if (Enum.TryParse<ProviderType>(value.Trim(), ignoreCase: true, out var provider) &&
            provider is ProviderType.IMAP or ProviderType.M365 or ProviderType.MSA)
        {
            return provider;
        }

        Add(errors, "provider", "The provider must be one of: IMAP, M365, MSA.");
        return ProviderType.IMAP;
    }

    private static void ValidateName(string? name, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            Add(errors, "name", "A name is required.");
        }
        else if (name.Trim().Length > MaxNameLength)
        {
            Add(errors, "name", $"The name must not exceed {MaxNameLength} characters.");
        }
    }

    private static void ValidateAddress(string? address, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            Add(errors, "emailAddress", "An e-mail address is required.");
        }
        else if (address.Trim().Length > MaxAddressLength || !AddressPattern.IsMatch(address.Trim()))
        {
            Add(errors, "emailAddress", "The e-mail address is not valid.");
        }
    }

    private static void ValidateImap(
        string? imapServer, int? imapPort, string? username,
        Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(imapServer))
        {
            Add(errors, "imapServer", "An IMAP server is required for IMAP accounts.");
        }
        else if (imapServer.Trim().Length > MaxHostLength)
        {
            Add(errors, "imapServer", $"The IMAP server must not exceed {MaxHostLength} characters.");
        }

        if (imapPort is < 1 or > 65535)
        {
            Add(errors, "imapPort", "The IMAP port must be between 1 and 65535.");
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            Add(errors, "username", "A username is required for IMAP accounts.");
        }
        else if (username.Trim().Length > MaxHostLength)
        {
            Add(errors, "username", $"The username must not exceed {MaxHostLength} characters.");
        }
    }

    private static void RequireSecret(string? value, string field, string message, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Add(errors, field, message);
        }
        else if (value.Length > MaxSecretLength)
        {
            Add(errors, field, $"The value must not exceed {MaxSecretLength} characters.");
        }
    }

    private static void ValidateRetention(int? deleteAfterDays, int? localRetentionDays, Dictionary<string, string[]> errors)
    {
        if (deleteAfterDays is < 1)
        {
            Add(errors, "deleteAfterDays", "deleteAfterDays must be at least 1.");
        }

        if (localRetentionDays is < 1)
        {
            Add(errors, "localRetentionDays", "localRetentionDays must be at least 1.");
        }

        if (localRetentionDays.HasValue && !deleteAfterDays.HasValue)
        {
            Add(errors, "localRetentionDays",
                "localRetentionDays requires deleteAfterDays: local retention only applies after server-side deletion.");
        }

        if (localRetentionDays.HasValue && deleteAfterDays.HasValue && localRetentionDays < deleteAfterDays)
        {
            Add(errors, "localRetentionDays",
                "localRetentionDays must be greater than or equal to deleteAfterDays.");
        }
    }

    private static void ValidateSync(int? syncIntervalMinutes, int? fullSyncIntervalHours, Dictionary<string, string[]> errors)
    {
        if (syncIntervalMinutes is < 1)
        {
            Add(errors, "syncIntervalMinutes", "syncIntervalMinutes must be at least 1.");
        }

        if (fullSyncIntervalHours is < 1)
        {
            Add(errors, "fullSyncIntervalHours", "fullSyncIntervalHours must be at least 1.");
        }
    }

    private static void ValidateExcludedFolders(List<string>? folders, Dictionary<string, string[]> errors)
    {
        if (folders == null)
        {
            return;
        }

        if (folders.Count > MaxExcludedFolders)
        {
            Add(errors, "excludedFolders", $"At most {MaxExcludedFolders} folder paths are allowed.");
            return;
        }

        if (folders.Any(f => f == null || f.Trim().Length == 0 || f.Length > MaxFolderLength || f.Contains(';')))
        {
            Add(errors, "excludedFolders",
                $"Each folder path must be 1-{MaxFolderLength} characters and must not contain ';'.");
        }
    }

    /// <summary>Stores folder paths in the same ';'-separated form the web UI uses.</summary>
    public static string JoinFolders(List<string>? folders)
        => folders == null || folders.Count == 0
            ? string.Empty
            : string.Join(";", folders.Select(f => f.Trim()).Where(f => f.Length > 0).Distinct());

    /// <summary>
    /// Seeds an update request with the current state of an account, so a PATCH
    /// that omits a property keeps its value. Secrets are left null, which means
    /// "keep the stored secret".
    /// </summary>
    public static UpdateMailAccountRequest FromEntity(MailAccount account)
    {
        return new UpdateMailAccountRequest
        {
            Name = account.Name,
            IsEnabled = account.IsEnabled,
            ImapServer = account.ImapServer,
            ImapPort = account.ImapPort,
            Username = account.Username,
            Password = null,
            UseSsl = account.UseSSL,
            ExcludedFolders = account.ExcludedFoldersList,
            DeleteAfterDays = account.DeleteAfterDays,
            LocalRetentionDays = account.LocalRetentionDays,
            SyncIntervalMinutes = account.SyncIntervalMinutes,
            FullSyncIntervalHours = account.FullSyncIntervalHours,
            ClientId = account.ClientId,
            ClientSecret = null,
            TenantId = account.TenantId
        };
    }

    /// <summary>
    /// Merges a PATCH request onto the current state: only properties that are
    /// non-null in <paramref name="patch"/> are taken over. Use PUT to clear a
    /// nullable setting.
    /// </summary>
    public static UpdateMailAccountRequest Merge(MailAccount account, UpdateMailAccountRequest patch)
    {
        var merged = FromEntity(account);

        if (patch.Name != null) merged.Name = patch.Name;
        if (patch.IsEnabled.HasValue) merged.IsEnabled = patch.IsEnabled;
        if (patch.ImapServer != null) merged.ImapServer = patch.ImapServer;
        if (patch.ImapPort.HasValue) merged.ImapPort = patch.ImapPort;
        if (patch.Username != null) merged.Username = patch.Username;
        if (patch.Password != null) merged.Password = patch.Password;
        if (patch.UseSsl.HasValue) merged.UseSsl = patch.UseSsl;
        if (patch.ExcludedFolders != null) merged.ExcludedFolders = patch.ExcludedFolders;
        if (patch.DeleteAfterDays.HasValue) merged.DeleteAfterDays = patch.DeleteAfterDays;
        if (patch.LocalRetentionDays.HasValue) merged.LocalRetentionDays = patch.LocalRetentionDays;
        if (patch.SyncIntervalMinutes.HasValue) merged.SyncIntervalMinutes = patch.SyncIntervalMinutes;
        if (patch.FullSyncIntervalHours.HasValue) merged.FullSyncIntervalHours = patch.FullSyncIntervalHours;
        if (patch.ClientId != null) merged.ClientId = patch.ClientId;
        if (patch.ClientSecret != null) merged.ClientSecret = patch.ClientSecret;
        if (patch.TenantId != null) merged.TenantId = patch.TenantId;

        return merged;
    }

    private static void Add(Dictionary<string, string[]> errors, string field, string message)
        => errors[field] = new[] { message };
}

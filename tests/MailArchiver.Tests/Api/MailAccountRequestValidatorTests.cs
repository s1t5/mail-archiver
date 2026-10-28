using MailArchiver.Models;
using MailArchiver.Models.Api;
using Xunit;

namespace MailArchiver.Tests.Api;

/// <summary>
/// Unit tests for the provider-specific rules of
/// <see cref="MailAccountRequestValidator"/>.
/// </summary>
public class MailAccountRequestValidatorTests
{
    private static CreateMailAccountRequest ValidImap() => new()
    {
        Name = "Team Postfach",
        EmailAddress = "team@example.com",
        Provider = "IMAP",
        ImapServer = "imap.example.com",
        ImapPort = 993,
        Username = "team@example.com",
        Password = "secret",
        UseSsl = true
    };

    [Fact]
    public void ValidateCreate_ValidImap_HasNoErrors()
    {
        var errors = MailAccountRequestValidator.ValidateCreate(ValidImap(), msaHasDefaultClientId: false);

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateCreate_MissingNameAndAddress_ReportsBoth()
    {
        var request = ValidImap();
        request.Name = " ";
        request.EmailAddress = null;

        var errors = MailAccountRequestValidator.ValidateCreate(request, false);

        Assert.Contains("name", errors.Keys);
        Assert.Contains("emailAddress", errors.Keys);
    }

    [Fact]
    public void ValidateCreate_InvalidAddress_IsRejected()
    {
        var request = ValidImap();
        request.EmailAddress = "not-an-address";

        var errors = MailAccountRequestValidator.ValidateCreate(request, false);

        Assert.Contains("emailAddress", errors.Keys);
    }

    [Fact]
    public void ValidateCreate_ImapWithoutCredentials_ReportsRequiredFields()
    {
        var request = ValidImap();
        request.ImapServer = null;
        request.Username = null;
        request.Password = null;

        var errors = MailAccountRequestValidator.ValidateCreate(request, false);

        Assert.Contains("imapServer", errors.Keys);
        Assert.Contains("username", errors.Keys);
        Assert.Contains("password", errors.Keys);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(70000)]
    public void ValidateCreate_PortOutOfRange_IsRejected(int port)
    {
        var request = ValidImap();
        request.ImapPort = port;

        var errors = MailAccountRequestValidator.ValidateCreate(request, false);

        Assert.Contains("imapPort", errors.Keys);
    }

    [Fact]
    public void ValidateCreate_ImportProvider_IsRejected()
    {
        var request = ValidImap();
        request.Provider = "IMPORT";

        var errors = MailAccountRequestValidator.ValidateCreate(request, false);

        Assert.Contains("provider", errors.Keys);
    }

    [Fact]
    public void ValidateCreate_UnknownProvider_IsRejected()
    {
        var request = ValidImap();
        request.Provider = "carrier-pigeon";

        var errors = MailAccountRequestValidator.ValidateCreate(request, false);

        Assert.Contains("provider", errors.Keys);
    }

    [Fact]
    public void ValidateCreate_M365_RequiresOAuthFields()
    {
        var request = ValidImap();
        request.Provider = "M365";
        request.ClientId = null;
        request.ClientSecret = null;
        request.TenantId = null;

        var errors = MailAccountRequestValidator.ValidateCreate(request, false);

        Assert.Contains("clientId", errors.Keys);
        Assert.Contains("clientSecret", errors.Keys);
        Assert.Contains("tenantId", errors.Keys);
    }

    [Fact]
    public void ValidateCreate_M365_WithOAuthFields_IsValid()
    {
        var request = ValidImap();
        request.Provider = "M365";
        request.ClientId = "client";
        request.ClientSecret = "secret";
        request.TenantId = "tenant";

        var errors = MailAccountRequestValidator.ValidateCreate(request, false);

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateCreate_MsaWithoutClientId_IsRejectedWhenNoDefaultExists()
    {
        var request = ValidImap();
        request.Provider = "MSA";
        request.ClientId = null;

        var errors = MailAccountRequestValidator.ValidateCreate(request, msaHasDefaultClientId: false);

        Assert.Contains("clientId", errors.Keys);
    }

    [Fact]
    public void ValidateCreate_MsaWithoutClientId_IsAcceptedWhenDefaultExists()
    {
        var request = ValidImap();
        request.Provider = "MSA";
        request.ClientId = null;

        var errors = MailAccountRequestValidator.ValidateCreate(request, msaHasDefaultClientId: true);

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateCreate_LocalRetentionWithoutDeleteAfterDays_IsRejected()
    {
        var request = ValidImap();
        request.LocalRetentionDays = 30;

        var errors = MailAccountRequestValidator.ValidateCreate(request, false);

        Assert.Contains("localRetentionDays", errors.Keys);
    }

    [Fact]
    public void ValidateCreate_LocalRetentionBelowDeleteAfterDays_IsRejected()
    {
        var request = ValidImap();
        request.DeleteAfterDays = 30;
        request.LocalRetentionDays = 10;

        var errors = MailAccountRequestValidator.ValidateCreate(request, false);

        Assert.Contains("localRetentionDays", errors.Keys);
    }

    [Fact]
    public void ValidateCreate_LocalRetentionAtLeastDeleteAfterDays_IsAccepted()
    {
        var request = ValidImap();
        request.DeleteAfterDays = 30;
        request.LocalRetentionDays = 30;

        var errors = MailAccountRequestValidator.ValidateCreate(request, false);

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateCreate_ExcludedFolderWithSeparator_IsRejected()
    {
        var request = ValidImap();
        request.ExcludedFolders = new List<string> { "INBOX;Spam" };

        var errors = MailAccountRequestValidator.ValidateCreate(request, false);

        Assert.Contains("excludedFolders", errors.Keys);
    }

    [Fact]
    public void ToEntity_Imap_AppliesDefaults()
    {
        var request = ValidImap();
        request.ImapPort = null;
        request.UseSsl = null;
        request.IsEnabled = null;

        var account = MailAccountRequestValidator.ToEntity(request);

        Assert.Equal(ProviderType.IMAP, account.Provider);
        Assert.Equal(993, account.ImapPort);
        Assert.True(account.UseSSL);
        Assert.True(account.IsEnabled);
        Assert.Equal("team@example.com", account.Username);
        Assert.Equal(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc), account.LastSync);
    }

    [Fact]
    public void ToEntity_StoresExcludedFoldersSemicolonSeparated()
    {
        var request = ValidImap();
        request.ExcludedFolders = new List<string> { "INBOX/Spam", "Trash" };

        var account = MailAccountRequestValidator.ToEntity(request);

        Assert.Equal("INBOX/Spam;Trash", account.ExcludedFolders);
        Assert.Equal(new[] { "INBOX/Spam", "Trash" }, account.ExcludedFoldersList);
    }

    [Fact]
    public void ValidateUpdate_MissingName_IsRejected()
    {
        var account = new MailAccount { Provider = ProviderType.IMAP, ImapServer = "imap", Username = "u", ImapPort = 993 };
        var request = new UpdateMailAccountRequest { Name = null };

        var errors = MailAccountRequestValidator.ValidateUpdate(account, request);

        Assert.Contains("name", errors.Keys);
    }

    [Fact]
    public void Apply_NullSecrets_KeepStoredValues()
    {
        var account = new MailAccount
        {
            Provider = ProviderType.IMAP,
            Name = "old",
            ImapServer = "imap.example.com",
            ImapPort = 993,
            Username = "u",
            Password = "stored",
            ClientSecret = "stored-secret"
        };

        MailAccountRequestValidator.Apply(new UpdateMailAccountRequest
        {
            Name = "new",
            ImapServer = "imap.example.com",
            ImapPort = 993,
            Username = "u",
            Password = null,
            ClientSecret = null
        }, account);

        Assert.Equal("new", account.Name);
        Assert.Equal("stored", account.Password);
        Assert.Equal("stored-secret", account.ClientSecret);
    }

    [Fact]
    public void Apply_EmptySecret_ClearsStoredValue()
    {
        var account = new MailAccount
        {
            Provider = ProviderType.IMAP,
            Name = "old",
            ImapServer = "imap.example.com",
            ImapPort = 993,
            Username = "u",
            Password = "stored"
        };

        MailAccountRequestValidator.Apply(new UpdateMailAccountRequest
        {
            Name = "old",
            ImapServer = "imap.example.com",
            ImapPort = 993,
            Username = "u",
            Password = ""
        }, account);

        Assert.Null(account.Password);
    }

    // ---- PATCH merge semantics -------------------------------------------------

    private static MailAccount MergeFixture() => new()
    {
        Provider = ProviderType.IMAP,
        Name = "Original",
        ImapServer = "imap.example.com",
        ImapPort = 993,
        Username = "user@example.com",
        Password = "stored",
        UseSSL = true,
        IsEnabled = true,
        DeleteAfterDays = 30,
        LocalRetentionDays = 90,
        ExcludedFolders = "Spam;Trash"
    };

    [Fact]
    public void Merge_OmittedProperties_KeepCurrentValues()
    {
        var account = MergeFixture();

        var merged = MailAccountRequestValidator.Merge(account, new UpdateMailAccountRequest { IsEnabled = false });

        Assert.False(merged.IsEnabled);
        Assert.Equal("Original", merged.Name);
        Assert.Equal(30, merged.DeleteAfterDays);
        Assert.Equal(993, merged.ImapPort);
        Assert.Null(merged.Password); // null = keep the stored secret
        Assert.Equal(new[] { "Spam", "Trash" }, merged.ExcludedFolders);
    }

    [Fact]
    public void Merge_EmptyExcludedFolders_ClearsTheList()
    {
        var account = MergeFixture();

        var merged = MailAccountRequestValidator.Merge(account,
            new UpdateMailAccountRequest { ExcludedFolders = new List<string>() });

        Assert.NotNull(merged.ExcludedFolders);
        Assert.Empty(merged.ExcludedFolders!);
        Assert.Equal(string.Empty, MailAccountRequestValidator.JoinFolders(merged.ExcludedFolders));
    }

    [Fact]
    public void Merge_Password_IsCarriedOver()
    {
        var account = MergeFixture();

        var merged = MailAccountRequestValidator.Merge(account,
            new UpdateMailAccountRequest { Password = "new-secret" });

        Assert.Equal("new-secret", merged.Password);
        Assert.Equal("Original", merged.Name);
    }

    [Fact]
    public void Merge_ResultOfMerge_PassesValidation()
    {
        var account = MergeFixture();

        var merged = MailAccountRequestValidator.Merge(account,
            new UpdateMailAccountRequest { Name = "Renamed", SyncIntervalMinutes = 15 });

        var errors = MailAccountRequestValidator.ValidateUpdate(account, merged);

        Assert.Empty(errors);
        Assert.Equal("Renamed", merged.Name);
        Assert.Equal(15, merged.SyncIntervalMinutes);
    }
}

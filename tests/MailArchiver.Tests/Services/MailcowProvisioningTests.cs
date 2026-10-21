using System.Security.Cryptography;
using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services.Mailcow;
using MailArchiver.Services.Security;
using MailArchiver.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace MailArchiver.Tests.Services;

[Collection(TestDbFixture.CollectionName)]
public class MailcowProvisioningTests(TestDbFixture fixture)
{
    [Fact]
    public void GeneratedPasswords_MeetMailcowCharacterClassRules()
    {
        var password = MailcowProvisioningService.GenerateAppPassword();
        Assert.True(password.Length >= 32);
        Assert.Contains(password, char.IsLower);
        Assert.Contains(password, char.IsUpper);
        Assert.Contains(password, char.IsDigit);
        Assert.Contains(password, c => !char.IsLetterOrDigit(c));
        Assert.NotEqual(password, MailcowProvisioningService.GenerateAppPassword());
    }

    private sealed class FakeApi : MailcowApiClient
    {
        public FakeApi() : base(new HttpClient()) { }
        public readonly List<MailcowAppPassword> Passwords = [];
        public string Address { get; } = Guid.NewGuid().ToString("N") + "@example.com";
        public bool TimeoutAfterCreate { get; set; }
        public bool FailDelete { get; set; }
        public int Creates { get; private set; }
        public override Task<List<MailcowRemoteMailbox>> GetMailboxesAsync(MailcowInstance i, CancellationToken ct) =>
            Task.FromResult(new List<MailcowRemoteMailbox> { new(Address, "Mailbox", true), new("disabled@example.com", "Disabled", false) });
        public override Task<List<MailcowAppPassword>> GetAppPasswordsAsync(MailcowInstance i, string address, CancellationToken ct) => Task.FromResult(Passwords.ToList());
        public override Task CreateAppPasswordAsync(MailcowInstance i, string address, string name, string password, CancellationToken ct)
        {
            Creates++;
            Passwords.Add(new("id-" + Creates, name));
            if (TimeoutAfterCreate) { TimeoutAfterCreate = false; throw new MailcowException("Timed out after creating remote password"); }
            return Task.CompletedTask;
        }
        public override Task DeleteAppPasswordAsync(MailcowInstance i, string id, CancellationToken ct)
        {
            if (FailDelete) throw new MailcowException("Mailcow unavailable");
            Passwords.RemoveAll(x => x.Id == id); return Task.CompletedTask;
        }
    }

    private sealed class Provisioner(MailArchiverDbContext db, MailcowApiClient api, ICredentialProtector protector)
        : MailcowProvisioningService(db, api, protector)
    {
        public bool FailImap { get; set; }
        public override Task VerifyImapAsync(MailcowInstance i, string address, string password, CancellationToken ct) =>
            FailImap ? Task.FromException(new MailcowException("IMAP unavailable")) : Task.CompletedTask;
    }

    [Fact]
    public async Task TimeoutRetry_DoesNotDuplicatePassword_EncryptsCredentials_AndRevokesOrphan()
    {
        var protector = new CredentialProtector(Options.Create(new SecurityOptions
        { CredentialEncryptionKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) }), NullLogger<CredentialProtector>.Instance);
        var options = new DbContextOptionsBuilder<MailArchiverDbContext>().UseNpgsql(fixture.ConnectionString)
            .ReplaceService<IModelCacheKeyFactory, CredentialModelCacheKeyFactory>().Options;
        await using var db = new MailArchiverDbContext(options, protector);
        var instance = MailcowApiClientTests.Instance();
        instance.ApiUrl = "https://" + Guid.NewGuid().ToString("N") + ".example.com/";
        db.MailcowInstances.Add(instance);
        await db.SaveChangesAsync();
        var api = new FakeApi { TimeoutAfterCreate = true };
        var service = new Provisioner(db, api, protector);
        int? accountId = null;
        try
        {
            await Assert.ThrowsAsync<MailcowException>(() => service.ImportAsync(instance, api.Address, default));
            var binding = await db.MailcowMailboxes.Include(x => x.MailAccount).SingleAsync(x => x.InstanceId == instance.Id);
            accountId = binding.MailAccountId;
            Assert.False(binding.MailAccount!.IsEnabled);
            var originalPassword = binding.MailAccount.Password;
            await service.ImportAsync(instance, api.Address, default);
            await service.ImportAsync(instance, api.Address, default);
            Assert.Equal(1, api.Creates);
            Assert.True(binding.MailAccount.IsEnabled);
            Assert.Equal(originalPassword, binding.MailAccount.Password);
            Assert.Equal("id-1", binding.AppPasswordId);

            await using var connection = new NpgsqlConnection(fixture.ConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("SELECT i.\"ApiKey\", a.\"Password\" FROM mail_archiver.\"MailcowInstances\" i JOIN mail_archiver.\"MailcowMailboxes\" b ON b.\"InstanceId\"=i.\"Id\" JOIN mail_archiver.\"MailAccounts\" a ON a.\"Id\"=b.\"MailAccountId\" WHERE i.\"Id\"=@id", connection);
            command.Parameters.AddWithValue("id", instance.Id);
            await using (var reader = await command.ExecuteReaderAsync())
            {
                Assert.True(await reader.ReadAsync());
                Assert.StartsWith("enc:v1:", reader.GetString(0));
                Assert.StartsWith("enc:v1:", reader.GetString(1));
            }

            db.MailAccounts.Remove(binding.MailAccount);
            await db.SaveChangesAsync();
            Assert.Null(binding.MailAccountId);
            await service.RevokeAsync(binding, default);
            Assert.Empty(api.Passwords);
            Assert.True(binding.IsRevoked);
        }
        finally
        {
            await db.MailcowMailboxes.Where(x => x.InstanceId == instance.Id).ExecuteDeleteAsync();
            if (accountId.HasValue) await db.MailAccounts.Where(x => x.Id == accountId.Value).ExecuteDeleteAsync();
            await db.MailcowInstances.Where(x => x.Id == instance.Id).ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task ImapFailure_RemainsDisabled_RetryReusesPassword_AndFailedRevocationIsRetryable()
    {
        var protector = new CredentialProtector(Options.Create(new SecurityOptions
        { CredentialEncryptionKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) }), NullLogger<CredentialProtector>.Instance);
        var options = new DbContextOptionsBuilder<MailArchiverDbContext>().UseNpgsql(fixture.ConnectionString)
            .ReplaceService<IModelCacheKeyFactory, CredentialModelCacheKeyFactory>().Options;
        await using var db = new MailArchiverDbContext(options, protector);
        var instance = MailcowApiClientTests.Instance();
        instance.ApiUrl = "https://" + Guid.NewGuid().ToString("N") + ".example.com/";
        db.MailcowInstances.Add(instance);
        await db.SaveChangesAsync();
        var api = new FakeApi();
        var service = new Provisioner(db, api, protector) { FailImap = true };
        int? accountId = null;
        try
        {
            await Assert.ThrowsAsync<MailcowException>(() => service.ImportAsync(instance, api.Address, default));
            var binding = await db.MailcowMailboxes.Include(x => x.MailAccount).SingleAsync(x => x.InstanceId == instance.Id);
            accountId = binding.MailAccountId;
            Assert.False(binding.MailAccount!.IsEnabled);
            service.FailImap = false;
            await service.ImportAsync(instance, api.Address, default);
            Assert.True(binding.MailAccount.IsEnabled);
            Assert.Equal(1, api.Creates);
            api.FailDelete = true;
            await Assert.ThrowsAsync<MailcowException>(() => service.RevokeAsync(binding, default));
            Assert.False(binding.MailAccount.IsEnabled);
            Assert.False(binding.IsRevoked);
            Assert.NotEmpty(binding.MailAccount.Password!);
            api.FailDelete = false;
            await service.RevokeAsync(binding, default);
            Assert.True(binding.IsRevoked);
            Assert.Null(binding.MailAccount.Password);
            Assert.Empty(api.Passwords);
            // A new activation after explicit revocation gets a fresh remote credential.
            await service.ImportAsync(instance, api.Address, default);
            Assert.True(binding.MailAccount.IsEnabled);
            Assert.Equal(2, api.Creates);
        }
        finally
        {
            await db.MailcowMailboxes.Where(x => x.InstanceId == instance.Id).ExecuteDeleteAsync();
            if (accountId.HasValue) await db.MailAccounts.Where(x => x.Id == accountId.Value).ExecuteDeleteAsync();
            await db.MailcowInstances.Where(x => x.Id == instance.Id).ExecuteDeleteAsync();
        }
    }

    [Theory]
    [InlineData("disabled@example.com")]
    [InlineData("not-listed@example.com")]
    public async Task PostedMailboxMustBeActiveAndListed(string address)
    {
        var protector = new CredentialProtector(Options.Create(new SecurityOptions
        { CredentialEncryptionKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) }), NullLogger<CredentialProtector>.Instance);
        await using var db = fixture.CreateContext();
        var api = new FakeApi();
        var service = new Provisioner(db, api, protector);
        await Assert.ThrowsAsync<MailcowException>(() => service.ImportAsync(MailcowApiClientTests.Instance(), address, default));
        Assert.Equal(0, api.Creates);
    }

    [Fact]
    public async Task EncryptionDisabled_RejectsImportBeforeCreatingAnything()
    {
        await using var db = fixture.CreateContext();
        var api = new FakeApi();
        var service = new Provisioner(db, api, NullCredentialProtector.Instance);
        await Assert.ThrowsAsync<MailcowException>(() => service.ImportAsync(MailcowApiClientTests.Instance(), api.Address, default));
        Assert.Equal(0, api.Creates);
    }

    private (MailArchiverDbContext Db, CredentialProtector Protector) CreateProtectedContext()
    {
        var protector = new CredentialProtector(Options.Create(new SecurityOptions
        { CredentialEncryptionKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) }), NullLogger<CredentialProtector>.Instance);
        var options = new DbContextOptionsBuilder<MailArchiverDbContext>().UseNpgsql(fixture.ConnectionString)
            .ReplaceService<IModelCacheKeyFactory, CredentialModelCacheKeyFactory>().Options;
        return (new MailArchiverDbContext(options, protector), protector);
    }

    [Fact]
    public async Task LinkExistingImapAccount_ReplacesCredentialsAndCreatesBinding()
    {
        var (db, protector) = CreateProtectedContext();
        await using var _ = db;
        var instance = MailcowApiClientTests.Instance();
        instance.ApiUrl = "https://" + Guid.NewGuid().ToString("N") + ".example.com/";
        var api = new FakeApi();
        var account = new MailAccount
        {
            Name = "Existing IMAP", EmailAddress = api.Address, Provider = ProviderType.IMAP,
            ImapServer = "old.example.com", ImapPort = 143, Username = "old-user",
            Password = "old-password", UseSSL = false, IsEnabled = false,
            LastSync = DateTime.UnixEpoch
        };
        db.MailcowInstances.Add(instance);
        db.MailAccounts.Add(account);
        await db.SaveChangesAsync();
        try
        {
            var service = new Provisioner(db, api, protector);
            await service.LinkAsync(instance, account.Id, default);

            var binding = await db.MailcowMailboxes.Include(x => x.MailAccount)
                .SingleAsync(x => x.MailAccountId == account.Id);
            Assert.Equal(instance.Id, binding.InstanceId);
            Assert.False(binding.IsRevoked);
            Assert.NotNull(binding.AppPasswordId);
            Assert.Equal(api.Address, binding.EmailAddress);
            Assert.Equal(instance.ImapHost, account.ImapServer);
            Assert.Equal(instance.ImapPort, account.ImapPort);
            Assert.Equal(api.Address, account.Username);
            Assert.NotEqual("old-password", account.Password);
            Assert.True(account.IsEnabled);
            Assert.Single(api.Passwords);
        }
        finally
        {
            await db.MailcowMailboxes.Where(x => x.MailAccountId == account.Id).ExecuteDeleteAsync();
            await db.MailAccounts.Where(x => x.Id == account.Id).ExecuteDeleteAsync();
            await db.MailcowInstances.Where(x => x.Id == instance.Id).ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task Detach_RevokesPasswordAndClearsAccountCredentials()
    {
        var (db, protector) = CreateProtectedContext();
        await using var _ = db;
        var instance = MailcowApiClientTests.Instance();
        instance.ApiUrl = "https://" + Guid.NewGuid().ToString("N") + ".example.com/";
        var api = new FakeApi();
        var account = new MailAccount
        {
            Name = "Existing IMAP", EmailAddress = api.Address, Provider = ProviderType.IMAP,
            ImapServer = "old.example.com", ImapPort = 143, Username = "old-user",
            Password = "old-password", UseSSL = false, IsEnabled = true,
            LastSync = DateTime.UnixEpoch
        };
        db.MailcowInstances.Add(instance);
        db.MailAccounts.Add(account);
        await db.SaveChangesAsync();
        try
        {
            var service = new Provisioner(db, api, protector);
            await service.LinkAsync(instance, account.Id, default);
            await service.DetachAsync(account, default);

            var binding = await db.MailcowMailboxes.SingleAsync(x => x.MailAccountId == account.Id);
            Assert.True(binding.IsRevoked);
            Assert.Null(account.Password);
            Assert.False(account.IsEnabled);
            Assert.Empty(api.Passwords);
        }
        finally
        {
            await db.MailcowMailboxes.Where(x => x.MailAccountId == account.Id).ExecuteDeleteAsync();
            await db.MailAccounts.Where(x => x.Id == account.Id).ExecuteDeleteAsync();
            await db.MailcowInstances.Where(x => x.Id == instance.Id).ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task LinkExistingAccount_RejectsNonImapProvider()
    {
        var (db, protector) = CreateProtectedContext();
        await using var _ = db;
        var instance = MailcowApiClientTests.Instance();
        instance.ApiUrl = "https://" + Guid.NewGuid().ToString("N") + ".example.com/";
        var api = new FakeApi();
        var account = new MailAccount
        {
            Name = "M365", EmailAddress = api.Address, Provider = ProviderType.M365,
            ClientId = "client", TenantId = "tenant", IsEnabled = true,
            LastSync = DateTime.UnixEpoch
        };
        db.MailcowInstances.Add(instance);
        db.MailAccounts.Add(account);
        await db.SaveChangesAsync();
        try
        {
            var service = new Provisioner(db, api, protector);
            await Assert.ThrowsAsync<MailcowException>(() => service.LinkAsync(instance, account.Id, default));
            Assert.Equal(0, api.Creates);
            Assert.False(await db.MailcowMailboxes.AnyAsync(x => x.MailAccountId == account.Id));
        }
        finally
        {
            await db.MailAccounts.Where(x => x.Id == account.Id).ExecuteDeleteAsync();
            await db.MailcowInstances.Where(x => x.Id == instance.Id).ExecuteDeleteAsync();
        }
    }
}

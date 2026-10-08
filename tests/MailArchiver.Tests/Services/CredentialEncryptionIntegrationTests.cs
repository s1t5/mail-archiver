using System.Security.Cryptography;
using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services;
using MailArchiver.Services.Security;
using MailArchiver.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MailArchiver.Tests.Services;

[Collection(TestDbFixture.CollectionName)]
public class CredentialEncryptionIntegrationTests
{
    private readonly TestDbFixture _fixture;

    public CredentialEncryptionIntegrationTests(TestDbFixture fixture) => _fixture = fixture;

    private static string NewKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private static CredentialProtector CreateProtector(string key)
        => new(Options.Create(new SecurityOptions { CredentialEncryptionKey = key }),
            NullLogger<CredentialProtector>.Instance);

    private DbContextOptions<MailArchiverDbContext> BuildEncryptedOptions()
        => new DbContextOptionsBuilder<MailArchiverDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .ReplaceService<IModelCacheKeyFactory, CredentialModelCacheKeyFactory>()
            .Options;

    private static MailAccount NewAccount(string name, string email, string? password)
        => new()
        {
            Name = name,
            EmailAddress = email,
            Username = email,
            Password = password,
            ImapServer = "imap.example.com",
            ImapPort = 993,
            UseSSL = true,
            Provider = ProviderType.IMAP,
            LastSync = DateTime.UtcNow
        };

    [Fact]
    public async Task Save_Encrypts_And_Read_Decrypts()
    {
        var options = BuildEncryptedOptions();
        var protector = CreateProtector(NewKey());
        var email = $"enc-{Guid.NewGuid():N}@example.com";
        int id;

        await using (var context = new MailArchiverDbContext(options, protector))
        {
            var account = NewAccount("enc-test", email, "sup3r-secret");
            context.MailAccounts.Add(account);
            await context.SaveChangesAsync();
            id = account.Id;
        }

        await using (var context = new MailArchiverDbContext(options, protector))
        {
            var raw = await ReadRawPasswordAsync(context, id);
            Assert.NotNull(raw);
            Assert.StartsWith("enc:v1:", raw);

            var reloaded = await context.MailAccounts.AsNoTracking().SingleAsync(a => a.Id == id);
            Assert.Equal("sup3r-secret", reloaded.Password);
        }

        await DeleteAccountAsync(id);
    }

    [Fact]
    public async Task Backfill_Mechanism_EncryptsPlaintextInPlace()
    {
        var options = BuildEncryptedOptions();
        var protector = CreateProtector(NewKey());
        int id;

        // Plain text is written by the fixture context (no protector).
        await using (var context = _fixture.CreateContext())
        {
            var account = NewAccount("backfill-test", $"backfill-{Guid.NewGuid():N}@example.com", "plain-secret");
            context.MailAccounts.Add(account);
            await context.SaveChangesAsync();
            id = account.Id;
        }

        // Same mechanism the backfill service uses: load, mark the property modified, save.
        await using (var context = new MailArchiverDbContext(options, protector))
        {
            var account = await context.MailAccounts.SingleAsync(a => a.Id == id);
            context.Entry(account).Property(a => a.Password).IsModified = true;
            await context.SaveChangesAsync();
        }

        await using (var context = new MailArchiverDbContext(options, protector))
        {
            var raw = await ReadRawPasswordAsync(context, id);
            Assert.NotNull(raw);
            Assert.StartsWith("enc:v1:", raw);

            var reloaded = await context.MailAccounts.AsNoTracking().SingleAsync(a => a.Id == id);
            Assert.Equal("plain-secret", reloaded.Password);
        }

        await DeleteAccountAsync(id);
    }

    [Fact]
    public async Task Backfill_Query_FindsOnlyPlaintext()
    {
        var options = BuildEncryptedOptions();
        var protector = CreateProtector(NewKey());
        int plainId;
        int encryptedId;

        await using (var context = _fixture.CreateContext())
        {
            var account = NewAccount("plain-test", $"plain-{Guid.NewGuid():N}@example.com", "plain-secret");
            context.MailAccounts.Add(account);
            await context.SaveChangesAsync();
            plainId = account.Id;
        }

        await using (var context = new MailArchiverDbContext(options, protector))
        {
            var account = NewAccount("encrypted-test", $"encrypted-{Guid.NewGuid():N}@example.com", "enc-secret");
            context.MailAccounts.Add(account);
            await context.SaveChangesAsync();
            encryptedId = account.Id;
        }

        await using (var context = new MailArchiverDbContext(options, protector))
        {
            var ids = await CredentialEncryptionBackfillService.FindPlaintextIdsAsync(context, CancellationToken.None);

            Assert.Contains(plainId, ids);
            Assert.DoesNotContain(encryptedId, ids);
        }

        await DeleteAccountAsync(plainId);
        await DeleteAccountAsync(encryptedId);
    }

    private async Task DeleteAccountAsync(int id)
    {
        await using var context = _fixture.CreateContext();
        await context.MailAccounts.Where(a => a.Id == id).ExecuteDeleteAsync();
    }

    private static async Task<string?> ReadRawPasswordAsync(MailArchiverDbContext context, int id)
    {
        var connection = context.Database.GetDbConnection();
        await context.Database.OpenConnectionAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT \"Password\" FROM mail_archiver.\"MailAccounts\" WHERE \"Id\" = @id";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@id";
        parameter.Value = id;
        command.Parameters.Add(parameter);

        var result = await command.ExecuteScalarAsync();
        return result as string;
    }
}

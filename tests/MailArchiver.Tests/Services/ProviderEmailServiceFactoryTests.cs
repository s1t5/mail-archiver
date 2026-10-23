using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services.Core;
using MailArchiver.Services.Factories;
using MailArchiver.Services.Providers;
using MailArchiver.Services.Providers.Imap;
using MailArchiver.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MailArchiver.Tests.Services;

/// <summary>
/// Integration tests for <see cref="ProviderEmailServiceFactory"/> that do NOT require
/// the full DI graph of the provider implementations. We verify the pure routing logic
/// (null- and unknown-account guards, unsupported provider type) and the account-based
/// lookup against the real PostgreSQL Dev database.
/// </summary>
[Collection(TestDbFixture.CollectionName)]
public class ProviderEmailServiceFactoryTests
{
    private readonly TestDbFixture _fixture;

    public ProviderEmailServiceFactoryTests(TestDbFixture fixture) => _fixture = fixture;

    private ProviderEmailServiceFactory CreateFactory(MailArchiverDbContext? context = null, IServiceProvider? provider = null)
    {
        context ??= _fixture.CreateContext();
        provider ??= new ServiceCollection().BuildServiceProvider();
        return new ProviderEmailServiceFactory(provider, context, NullLogger<ProviderEmailServiceFactory>.Instance);
    }

    private static IServiceProvider BuildProviderWithImapFake()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ImapEmailService>(_ => new FakeImapEmailService());
        return services.BuildServiceProvider();
    }

    [Fact]
    public void GetServiceForAccount_NullAccount_ThrowsArgumentNullException()
    {
        var factory = CreateFactory();
        Assert.Throws<ArgumentNullException>(() => factory.GetServiceForAccount((MailAccount)null!));
    }

    [Fact]
    public async Task GetServiceForAccountAsync_UnknownAccountId_ThrowsInvalidOperationException()
    {
        var factory = CreateFactory();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => factory.GetServiceForAccountAsync(int.MaxValue - 1));
    }

    [Fact]
    public void GetServiceForAccount_UnknownAccountId_ThrowsInvalidOperationException()
    {
        var factory = CreateFactory();
        Assert.Throws<InvalidOperationException>(
            () => factory.GetServiceForAccount(int.MaxValue - 1));
    }

    [Fact]
    public async Task GetServiceForAccountAsync_ExistingAccount_RoutesByProvider()
    {
        // Seed a temporary IMAP account, then verify the factory routes to ImapEmailService.
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var account = new MailAccount
            {
                Name = "TestFactoryAccount",
                EmailAddress = "factory-test@example.com",
                Provider = ProviderType.IMAP,
                ImapServer = "imap.example.com",
                ImapPort = 993,
                IsEnabled = true,
                LastSync = DateTime.UtcNow
            };
            ctx.MailAccounts.Add(account);
            await ctx.SaveChangesAsync();

            var factory = CreateFactory(ctx, BuildProviderWithImapFake());
            var service = factory.GetServiceForAccount(account.Id);
            Assert.IsType<FakeImapEmailService>(service);
        }
        finally
        {
            await scope.RollbackAsync();
        }
    }

    [Fact]
    public void GetService_UnsupportedProviderType_ThrowsNotSupportedException()
    {
        var factory = CreateFactory(provider: new ServiceCollection().BuildServiceProvider());
        Assert.Throws<NotSupportedException>(
            () => factory.GetService((ProviderType)999));
    }

    /// <summary>
    /// A no-op subclass of <see cref="ImapEmailService"/> that passes null dependencies,
    /// used solely to verify provider routing without standing up the full IMAP stack.
    /// </summary>
    private sealed class FakeImapEmailService : ImapEmailService
    {
        public FakeImapEmailService()
            : base(null!, null!, null!, null!, null!, null!, NullLogger<ImapEmailService>.Instance) { }
    }
}
using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services;
using MailArchiver.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MailArchiver.Tests.Services;

/// <summary>
/// Integration tests for <see cref="ApiKeyService"/> against the PostgreSQL Dev database.
/// Each test runs inside a rolled-back transaction so no rows persist.
/// </summary>
[Collection(TestDbFixture.CollectionName)]
public class ApiKeyServiceTests
{
    private readonly TestDbFixture _fixture;

    public ApiKeyServiceTests(TestDbFixture fixture) => _fixture = fixture;

    private static async Task<User> SeedUserAsync(MailArchiverDbContext ctx)
    {
        var user = new User
        {
            Username = $"u-{Guid.NewGuid():N}".Substring(0, 30),
            Email = $"u{Guid.NewGuid():N}@test.local",
            IsActive = true,
            IsAdmin = false,
            IsSelfManager = false
        };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task CreateAsync_SetsHashAndPrefix_ReturnsPlaintextKey()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
            var ctx = scope.Context;
        try
        {
            var user = await SeedUserAsync(ctx);
            var svc = new ApiKeyService(ctx);

            var (entity, plaintext) = await svc.CreateAsync(user.Id, "ci-key", expiresAt: null);

            Assert.Equal("ci-key", entity.Name);
            Assert.Equal(user.Id, entity.UserId);
            Assert.StartsWith(ApiKeyService.KeyPrefixMarker, plaintext);
            Assert.Equal(ApiKeyService.ComputeHash(plaintext), entity.KeyHash);
            Assert.Equal(plaintext.Substring(0, ApiKeyService.StoredPrefixLength), entity.KeyPrefix);
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task ValidateAsync_ValidKey_ReturnsUser()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
            var ctx = scope.Context;
        try
        {
            var user = await SeedUserAsync(ctx);
            var svc = new ApiKeyService(ctx);
            var (_, plaintext) = await svc.CreateAsync(user.Id, "v", null);

            var validated = await svc.ValidateAsync(plaintext);
            Assert.NotNull(validated);
            Assert.Equal(user.Id, validated!.Id);
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task ValidateAsync_BogusKey_ReturnsNull()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
            var ctx = scope.Context;
        try
        {
            var svc = new ApiKeyService(ctx);
            Assert.Null(await svc.ValidateAsync("ma_bogus_bogus_bogus_bogus_bogus_bogus"));
        }
        finally { await scope.RollbackAsync(); }
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task ValidateAsync_EmptyOrNull_ReturnsNull(string? key)
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
            var ctx = scope.Context;
        try
        {
            var svc = new ApiKeyService(ctx);
            Assert.Null(await svc.ValidateAsync(key!));
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task ValidateAsync_KeyWithoutPrefix_ReturnsNull()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
            var ctx = scope.Context;
        try
        {
            var svc = new ApiKeyService(ctx);
            Assert.Null(await svc.ValidateAsync("no-prefix-here-1234567890"));
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task ValidateAsync_RevokedKey_ReturnsNull()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
            var ctx = scope.Context;
        try
        {
            var user = await SeedUserAsync(ctx);
            var svc = new ApiKeyService(ctx);
            var (entity, plaintext) = await svc.CreateAsync(user.Id, "rev", null);

            Assert.True(await svc.RevokeAsync(entity.Id, user.Id, isAdmin: false));
            Assert.Null(await svc.ValidateAsync(plaintext));
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task ValidateAsync_ExpiredKey_ReturnsNull()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
            var ctx = scope.Context;
        try
        {
            var user = await SeedUserAsync(ctx);
            var svc = new ApiKeyService(ctx);
            var (_, plaintext) = await svc.CreateAsync(user.Id, "exp", DateTime.UtcNow.AddMinutes(-1));

            Assert.Null(await svc.ValidateAsync(plaintext));
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task ValidateAsync_InactiveUser_ReturnsNull()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
            var ctx = scope.Context;
        try
        {
            var user = await SeedUserAsync(ctx);
            var svc = new ApiKeyService(ctx);
            var (_, plaintext) = await svc.CreateAsync(user.Id, "u-inactive", null);

            user.IsActive = false;
            await ctx.SaveChangesAsync();

            Assert.Null(await svc.ValidateAsync(plaintext));
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task RevokeAsync_OwnerUser_Succeeds()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
            var ctx = scope.Context;
        try
        {
            var user = await SeedUserAsync(ctx);
            var svc = new ApiKeyService(ctx);
            var (entity, _) = await svc.CreateAsync(user.Id, "r", null);

            Assert.True(await svc.RevokeAsync(entity.Id, user.Id, isAdmin: false));
            Assert.NotNull((await ctx.ApiKeys.FindAsync(entity.Id))!.RevokedAt);
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task RevokeAsync_NonOwnerNonAdmin_ReturnsFalse()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
            var ctx = scope.Context;
        try
        {
            var user = await SeedUserAsync(ctx);
            var other = await SeedUserAsync(ctx);
            var svc = new ApiKeyService(ctx);
            var (entity, _) = await svc.CreateAsync(user.Id, "r2", null);

            Assert.False(await svc.RevokeAsync(entity.Id, other.Id, isAdmin: false));
            Assert.Null((await ctx.ApiKeys.FindAsync(entity.Id))!.RevokedAt);
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task RevokeAsync_AdminForOtherUser_Succeeds()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
            var ctx = scope.Context;
        try
        {
            var user = await SeedUserAsync(ctx);
            var admin = await SeedUserAsync(ctx);
            var svc = new ApiKeyService(ctx);
            var (entity, _) = await svc.CreateAsync(user.Id, "r-admin", null);

            Assert.True(await svc.RevokeAsync(entity.Id, admin.Id, isAdmin: true));
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task RevokeAsync_AlreadyRevoked_IsIdempotent()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
            var ctx = scope.Context;
        try
        {
            var user = await SeedUserAsync(ctx);
            var svc = new ApiKeyService(ctx);
            var (entity, _) = await svc.CreateAsync(user.Id, "idem", null);

            Assert.True(await svc.RevokeAsync(entity.Id, user.Id, false));
            Assert.True(await svc.RevokeAsync(entity.Id, user.Id, false));
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task RevokeAsync_UnknownKeyId_ReturnsFalse()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
            var ctx = scope.Context;
        try
        {
            var svc = new ApiKeyService(ctx);
            Assert.False(await svc.RevokeAsync(int.MaxValue - 1, 1, true));
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task GetKeysForUserAsync_ReturnsOnlyOwnKeysOrderedByCreatedDesc()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
            var ctx = scope.Context;
        try
        {
            var user = await SeedUserAsync(ctx);
            var other = await SeedUserAsync(ctx);
            var svc = new ApiKeyService(ctx);
            await svc.CreateAsync(user.Id, "k1", null);
            await Task.Delay(15);
            await svc.CreateAsync(user.Id, "k2", null);
            await svc.CreateAsync(other.Id, "other", null);

            var keys = await svc.GetKeysForUserAsync(user.Id);
            Assert.Equal(2, keys.Count);
            Assert.All(keys, k => Assert.Equal(user.Id, k.UserId));
            Assert.True(keys[0].CreatedAt >= keys[1].CreatedAt);
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task GetAllKeysAsync_ReturnsAllKeysWithUsers()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
            var ctx = scope.Context;
        try
        {
            var user = await SeedUserAsync(ctx);
            var svc = new ApiKeyService(ctx);
            await svc.CreateAsync(user.Id, "all", null);

            var keys = await svc.GetAllKeysAsync();
            Assert.NotEmpty(keys);
            // User navigation should be loaded by Include().
            Assert.Contains(keys, k => k.User?.Id == user.Id);
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task ValidateAsync_ThrottlesLastUsedAtWrites()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
            var ctx = scope.Context;
        try
        {
            var user = await SeedUserAsync(ctx);
            var svc = new ApiKeyService(ctx);
            var (entity, plaintext) = await svc.CreateAsync(user.Id, "throttle", null);

            await svc.ValidateAsync(plaintext);
            var firstUsed = (await ctx.ApiKeys.AsNoTracking().FirstAsync(k => k.Id == entity.Id)).LastUsedAt;
            Assert.NotNull(firstUsed);

            await svc.ValidateAsync(plaintext); // within 5-minute window, should not write
            var secondUsed = (await ctx.ApiKeys.AsNoTracking().FirstAsync(k => k.Id == entity.Id)).LastUsedAt;
            Assert.Equal(firstUsed, secondUsed);
        }
        finally { await scope.RollbackAsync(); }
    }
}
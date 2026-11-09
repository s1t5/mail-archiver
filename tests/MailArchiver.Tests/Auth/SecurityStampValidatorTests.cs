using MailArchiver.Auth.Helpers;
using MailArchiver.Auth.Services;
using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Xunit;

namespace MailArchiver.Tests.Auth;

/// <summary>
/// Regression tests for the security stamp check. Before it existed, a cookie stayed
/// valid until its own expiry, so a password change, a deactivation or a deletion did
/// not end sessions that were already open elsewhere.
/// </summary>
[Collection(TestDbFixture.CollectionName)]
public class SecurityStampValidatorTests
{
    private readonly TestDbFixture _fixture;

    public SecurityStampValidatorTests(TestDbFixture fixture) => _fixture = fixture;

    private static async Task<User> SeedUserAsync(MailArchiverDbContext ctx, bool isActive = true)
    {
        var user = new User
        {
            Username = $"stamp-{Guid.NewGuid():N}"[..20],
            Email = $"stamp{Guid.NewGuid():N}@test.local",
            IsActive = isActive
        };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        return user;
    }

    private static ClaimsPrincipal Principal(int userId, string? stamp)
    {
        var claims = new List<Claim> { new(AuthClaimTypes.UserId, userId.ToString()) };
        if (stamp is not null)
        {
            claims.Add(new Claim(AuthClaimTypes.SecurityStamp, stamp));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    [Fact]
    public async Task MatchingStamp_ActiveUser_IsValid()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var user = await SeedUserAsync(ctx);

            Assert.True(await SecurityStampValidator.IsSessionValidAsync(
                Principal(user.Id, user.SecurityStamp), ctx));
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task RotatedStamp_InvalidatesExistingCookie()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var user = await SeedUserAsync(ctx);
            var issuedStamp = user.SecurityStamp;

            user.SecurityStamp = Guid.NewGuid().ToString("N");
            await ctx.SaveChangesAsync();

            Assert.False(await SecurityStampValidator.IsSessionValidAsync(
                Principal(user.Id, issuedStamp), ctx));
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task DeactivatedUser_IsInvalidEvenWithoutRotation()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var user = await SeedUserAsync(ctx);
            var issuedStamp = user.SecurityStamp;

            // Direct database change, as an operator might do it, without touching the stamp.
            user.IsActive = false;
            await ctx.SaveChangesAsync();

            Assert.False(await SecurityStampValidator.IsSessionValidAsync(
                Principal(user.Id, issuedStamp), ctx));
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task DeletedUser_IsInvalid()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var user = await SeedUserAsync(ctx);
            var issuedStamp = user.SecurityStamp;
            var userId = user.Id;

            ctx.Users.Remove(user);
            await ctx.SaveChangesAsync();

            Assert.False(await SecurityStampValidator.IsSessionValidAsync(
                Principal(userId, issuedStamp), ctx));
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task CookieWithoutStamp_IsInvalid()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var user = await SeedUserAsync(ctx);

            Assert.False(await SecurityStampValidator.IsSessionValidAsync(
                Principal(user.Id, stamp: null), ctx));
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task CookieWithoutValidUserId_IsInvalid()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(AuthClaimTypes.SecurityStamp, Guid.NewGuid().ToString("N")),
                new Claim(AuthClaimTypes.UserId, "not-a-number")
            }, "Test"));

            Assert.False(await SecurityStampValidator.IsSessionValidAsync(principal, ctx));
        }
        finally { await scope.RollbackAsync(); }
    }
}

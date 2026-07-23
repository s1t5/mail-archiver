using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using MailArchiver.Auth.Options;
using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services;
using MailArchiver.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MailArchiver.Tests.Services;

/// <summary>
/// Integration tests for <see cref="UserService"/> against the PostgreSQL Dev database.
/// Password hashing tests are pure (no DB); the rest run inside a rolled-back transaction.
/// </summary>
[Collection(TestDbFixture.CollectionName)]
public class UserServiceTests
{
    private readonly TestDbFixture _fixture;
    public UserServiceTests(TestDbFixture fixture) => _fixture = fixture;

    private static async Task<User> SeedUserAsync(MailArchiverDbContext ctx, string? username = null,
        bool isActive = true, string? oauthRemoteUserId = null, bool isAdmin = false,
        bool requiresApproval = false)
    {
        var rawName = username ?? $"u-{Guid.NewGuid():N}";
        var user = new User
        {
            Username = rawName.Length > 50 ? rawName.Substring(0, 50) : rawName,
            Email = $"u{Guid.NewGuid():N}@test.local",
            IsActive = isActive,
            IsAdmin = isAdmin,
            IsSelfManager = false,
            OAuthRemoteUserId = oauthRemoteUserId,
            RequiresApproval = requiresApproval
        };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        return user;
    }

    // ============================================================
    // Password Hashing (pure)
    // ============================================================

    [Fact]
    public void HashPassword_ProducesPbkdf2Format()
    {
        var svc = ServiceFactory.CreateUserService(_fixture.CreateContext());
        var hash = svc.HashPassword("secret");
        var parts = hash.Split('.', 3);
        Assert.Equal(3, parts.Length);
        Assert.Equal("600000", parts[0]); // iterations
        Assert.NotEmpty(parts[1]); // salt
        Assert.NotEmpty(parts[2]); // hash
    }

    [Fact]
    public void HashPassword_DifferentCalls_ProduceDifferentSalts()
    {
        var svc = ServiceFactory.CreateUserService(_fixture.CreateContext());
        var h1 = svc.HashPassword("secret");
        var h2 = svc.HashPassword("secret");
        Assert.NotEqual(h1, h2);
    }

    [Fact]
    public void VerifyPassword_CorrectPassword_ReturnsTrue()
    {
        var svc = ServiceFactory.CreateUserService(_fixture.CreateContext());
        var hash = svc.HashPassword("mypw");
        Assert.True(svc.VerifyPassword("mypw", hash));
    }

    [Fact]
    public void VerifyPassword_WrongPassword_ReturnsFalse()
    {
        var svc = ServiceFactory.CreateUserService(_fixture.CreateContext());
        var hash = svc.HashPassword("mypw");
        Assert.False(svc.VerifyPassword("wrong", hash));
    }

    [Fact]
    public void VerifyPassword_LegacySha256_VerifiesTrue()
    {
        // Build a legacy SHA256 hash manually
        var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes("legacypw"));
        var legacyHash = Convert.ToBase64String(bytes);

        var svc = ServiceFactory.CreateUserService(_fixture.CreateContext());
        Assert.True(svc.VerifyPassword("legacypw", legacyHash));
    }

    [Fact]
    public void VerifyPassword_EmptyStoredHash_ReturnsFalse()
    {
        var svc = ServiceFactory.CreateUserService(_fixture.CreateContext());
        Assert.False(svc.VerifyPassword("x", ""));
        Assert.False(svc.VerifyPassword("x", null!));
    }

    // ============================================================
    // CRUD
    // ============================================================

    [Fact]
    public async Task CreateUserAsync_PersistsWithHashedPassword()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var svc = ServiceFactory.CreateUserService(ctx);
            var user = await svc.CreateUserAsync("newuser", "new@test.local", "pw", isAdmin: true);
            Assert.True(user.Id > 0);
            Assert.NotEmpty(user.PasswordHash);
            Assert.True(user.IsAdmin);

            var stored = await ctx.Users.AsNoTracking().FirstAsync(u => u.Id == user.Id);
            Assert.Equal("newuser", stored.Username);
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task GetUserByIdAsync_ReturnsUser()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var user = await SeedUserAsync(ctx);
            var svc = ServiceFactory.CreateUserService(ctx);
            var found = await svc.GetUserByIdAsync(user.Id);
            Assert.NotNull(found);
            Assert.Equal(user.Username, found!.Username);
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task GetUserByUsernameAsync_CaseInsensitive()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var user = await SeedUserAsync(ctx, "MixedCase");
            var svc = ServiceFactory.CreateUserService(ctx);
            Assert.NotNull(await svc.GetUserByUsernameAsync("mixedcase"));
            Assert.NotNull(await svc.GetUserByUsernameAsync("MIXEDCASE"));
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task GetUserByUsernameAsync_Empty_ReturnsNull()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var svc = ServiceFactory.CreateUserService(ctx);
            Assert.Null(await svc.GetUserByUsernameAsync(""));
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task GetUserByEmailAsync_CaseInsensitive()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var user = await SeedUserAsync(ctx);
            var svc = ServiceFactory.CreateUserService(ctx);
            Assert.NotNull(await svc.GetUserByEmailAsync(user.Email.ToUpperInvariant()));
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task GetAllUsersAsync_ReturnsList()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            await SeedUserAsync(ctx);
            await SeedUserAsync(ctx);
            var svc = ServiceFactory.CreateUserService(ctx);
            var all = await svc.GetAllUsersAsync();
            Assert.True(all.Count >= 2);
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task UpdateUserAsync_Success()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var user = await SeedUserAsync(ctx);
            var svc = ServiceFactory.CreateUserService(ctx);
            user.IsAdmin = true;
            Assert.True(await svc.UpdateUserAsync(user));
            var refreshed = await ctx.Users.AsNoTracking().FirstAsync(u => u.Id == user.Id);
            Assert.True(refreshed.IsAdmin);
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task DeleteUserAsync_RemovesUserAndAssociations()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var user = await SeedUserAsync(ctx);
            var acct = new MailAccount
            {
                Name = "del-acct", EmailAddress = $"{Guid.NewGuid():N}@t.local",
                Provider = ProviderType.IMAP, IsEnabled = true, LastSync = DateTime.UtcNow
            };
            ctx.MailAccounts.Add(acct);
            await ctx.SaveChangesAsync();
            ctx.UserMailAccounts.Add(new UserMailAccount { UserId = user.Id, MailAccountId = acct.Id });
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateUserService(ctx);
            Assert.True(await svc.DeleteUserAsync(user.Id));
            Assert.Null(await ctx.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == user.Id));
            Assert.Empty(await ctx.UserMailAccounts.AsNoTracking()
                .Where(uma => uma.UserId == user.Id).ToListAsync());
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task DeleteUserAsync_Nonexistent_ReturnsFalse()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var svc = ServiceFactory.CreateUserService(ctx);
            Assert.False(await svc.DeleteUserAsync(int.MaxValue - 1));
        }
        finally { await scope.RollbackAsync(); }
    }

    // ============================================================
    // AuthenticateUserAsync
    // ============================================================

    [Fact]
    public async Task AuthenticateUserAsync_CorrectPassword_ReturnsTrueAndSetsLastLogin()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var svc = ServiceFactory.CreateUserService(ctx);
            var user = await svc.CreateUserAsync("authuser", "auth@test.local", "pw123");
            Assert.True(await svc.AuthenticateUserAsync("authuser", "pw123"));
            var refreshed = await ctx.Users.AsNoTracking().FirstAsync(u => u.Id == user.Id);
            Assert.NotNull(refreshed.LastLoginAt);
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task AuthenticateUserAsync_WrongPassword_ReturnsFalse()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var svc = ServiceFactory.CreateUserService(ctx);
            await svc.CreateUserAsync("authuser2", "auth2@test.local", "pw123");
            Assert.False(await svc.AuthenticateUserAsync("authuser2", "wrong"));
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task AuthenticateUserAsync_NonexistentUser_ReturnsFalse()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var svc = ServiceFactory.CreateUserService(ctx);
            Assert.False(await svc.AuthenticateUserAsync("ghost", "x"));
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task AuthenticateUserAsync_InactiveUser_ReturnsFalse()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var svc = ServiceFactory.CreateUserService(ctx);
            var user = await svc.CreateUserAsync("inactive", "in@test.local", "pw");
            user.IsActive = false;
            await ctx.SaveChangesAsync();
            Assert.False(await svc.AuthenticateUserAsync("inactive", "pw"));
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task AuthenticateUserAsync_OidcUser_ReturnsFalse()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var svc = ServiceFactory.CreateUserService(ctx);
            var user = await svc.CreateUserAsync("oidcuser", "oidc@test.local", "pw");
            user.OAuthRemoteUserId = "remote-123";
            await ctx.SaveChangesAsync();
            Assert.False(await svc.AuthenticateUserAsync("oidcuser", "pw"));
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task AuthenticateUserAsync_LegacySha256_UpgradesToPbkdf2()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var sha256 = SHA256.Create();
            var legacyHash = Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes("legacypw")));
            var user = new User
            {
                Username = "legacyuser", Email = "legacy@test.local",
                PasswordHash = legacyHash, IsActive = true
            };
            ctx.Users.Add(user);
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateUserService(ctx);
            Assert.True(await svc.AuthenticateUserAsync("legacyuser", "legacypw"));
            var refreshed = await ctx.Users.AsNoTracking().FirstAsync(u => u.Id == user.Id);
            // Upgraded to PBKDF2 format (2 dots)
            Assert.Equal(2, refreshed.PasswordHash.Count(c => c == '.'));
        }
        finally { await scope.RollbackAsync(); }
    }

    // ============================================================
    // SetUserActiveStatusAsync / Authorization
    // ============================================================

    [Fact]
    public async Task SetUserActiveStatusAsync_TogglesStatus()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var user = await SeedUserAsync(ctx, isActive: true);
            var svc = ServiceFactory.CreateUserService(ctx);
            Assert.True(await svc.SetUserActiveStatusAsync(user.Id, false));
            Assert.False((await ctx.Users.AsNoTracking().FirstAsync(u => u.Id == user.Id)).IsActive);
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task SetUserActiveStatusAsync_Nonexistent_ReturnsFalse()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var svc = ServiceFactory.CreateUserService(ctx);
            Assert.False(await svc.SetUserActiveStatusAsync(int.MaxValue - 1, true));
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task SetUserActiveStatusAsync_ActivatingOidcUser_ClearsRequiresApproval()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var user = await SeedUserAsync(ctx, isActive: false, oauthRemoteUserId: "remote", requiresApproval: true);
            var svc = ServiceFactory.CreateUserService(ctx);
            Assert.True(await svc.SetUserActiveStatusAsync(user.Id, true));
            var refreshed = await ctx.Users.AsNoTracking().FirstAsync(u => u.Id == user.Id);
            Assert.False(refreshed.RequiresApproval);
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task IsUserAdminAsync_Admin_True()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var user = await SeedUserAsync(ctx, isAdmin: true);
            var svc = ServiceFactory.CreateUserService(ctx);
            Assert.True(await svc.IsUserAdminAsync(user.Id));
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task IsUserAdminAsync_NonAdmin_False()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var user = await SeedUserAsync(ctx, isAdmin: false);
            var svc = ServiceFactory.CreateUserService(ctx);
            Assert.False(await svc.IsUserAdminAsync(user.Id));
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task IsUserAdminAsync_Nonexistent_False()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var svc = ServiceFactory.CreateUserService(ctx);
            Assert.False(await svc.IsUserAdminAsync(int.MaxValue - 1));
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task IsUserAuthorizedForAccountAsync_Admin_GrantsAll()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var admin = await SeedUserAsync(ctx, isAdmin: true);
            var acct = new MailAccount
            {
                Name = "a", EmailAddress = $"{Guid.NewGuid():N}@t.local",
                Provider = ProviderType.IMAP, IsEnabled = true, LastSync = DateTime.UtcNow
            };
            ctx.MailAccounts.Add(acct);
            await ctx.SaveChangesAsync();
            var svc = ServiceFactory.CreateUserService(ctx);
            Assert.True(await svc.IsUserAuthorizedForAccountAsync(admin.Id, acct.Id));
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task IsUserAuthorizedForAccountAsync_SelfManager_GrantsAll()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var user = await SeedUserAsync(ctx);
            user.IsSelfManager = true;
            await ctx.SaveChangesAsync();
            var acct = new MailAccount
            {
                Name = "sm", EmailAddress = $"{Guid.NewGuid():N}@t.local",
                Provider = ProviderType.IMAP, IsEnabled = true, LastSync = DateTime.UtcNow
            };
            ctx.MailAccounts.Add(acct);
            await ctx.SaveChangesAsync();
            var svc = ServiceFactory.CreateUserService(ctx);
            Assert.True(await svc.IsUserAuthorizedForAccountAsync(user.Id, acct.Id));
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task IsUserAuthorizedForAccountAsync_DirectAssignment_Grants()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var user = await SeedUserAsync(ctx);
            var acct = new MailAccount
            {
                Name = "direct", EmailAddress = $"{Guid.NewGuid():N}@t.local",
                Provider = ProviderType.IMAP, IsEnabled = true, LastSync = DateTime.UtcNow
            };
            ctx.MailAccounts.Add(acct);
            await ctx.SaveChangesAsync();
            ctx.UserMailAccounts.Add(new UserMailAccount { UserId = user.Id, MailAccountId = acct.Id });
            await ctx.SaveChangesAsync();
            var svc = ServiceFactory.CreateUserService(ctx);
            Assert.True(await svc.IsUserAuthorizedForAccountAsync(user.Id, acct.Id));
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task IsUserAuthorizedForAccountAsync_NoAssignment_Denies()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var user = await SeedUserAsync(ctx);
            var acct = new MailAccount
            {
                Name = "denied", EmailAddress = $"{Guid.NewGuid():N}@t.local",
                Provider = ProviderType.IMAP, IsEnabled = true, LastSync = DateTime.UtcNow
            };
            ctx.MailAccounts.Add(acct);
            await ctx.SaveChangesAsync();
            var svc = ServiceFactory.CreateUserService(ctx);
            Assert.False(await svc.IsUserAuthorizedForAccountAsync(user.Id, acct.Id));
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task GetAdminCountAsync_CountsActiveAdmins()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            await SeedUserAsync(ctx, isAdmin: true, isActive: true);
            await SeedUserAsync(ctx, isAdmin: true, isActive: false);
            await SeedUserAsync(ctx, isAdmin: false);
            var svc = ServiceFactory.CreateUserService(ctx);
            var count = await svc.GetAdminCountAsync();
            // At least one active admin (plus any existing in the DB).
            Assert.True(count >= 1);
        }
        finally { await scope.RollbackAsync(); }
    }

    // ============================================================
    // Mail-Account-Zuweisung
    // ============================================================

    [Fact]
    public async Task AssignMailAccountToUserAsync_New_Assigns()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var user = await SeedUserAsync(ctx);
            var acct = new MailAccount
            {
                Name = "assign", EmailAddress = $"{Guid.NewGuid():N}@t.local",
                Provider = ProviderType.IMAP, IsEnabled = true, LastSync = DateTime.UtcNow
            };
            ctx.MailAccounts.Add(acct);
            await ctx.SaveChangesAsync();
            var svc = ServiceFactory.CreateUserService(ctx);
            Assert.True(await svc.AssignMailAccountToUserAsync(user.Id, acct.Id));
            Assert.True(await ctx.UserMailAccounts.AnyAsync(uma => uma.UserId == user.Id && uma.MailAccountId == acct.Id));
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task AssignMailAccountToUserAsync_AlreadyAssigned_ReturnsTrue()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var user = await SeedUserAsync(ctx);
            var acct = new MailAccount
            {
                Name = "dup", EmailAddress = $"{Guid.NewGuid():N}@t.local",
                Provider = ProviderType.IMAP, IsEnabled = true, LastSync = DateTime.UtcNow
            };
            ctx.MailAccounts.Add(acct);
            await ctx.SaveChangesAsync();
            var svc = ServiceFactory.CreateUserService(ctx);
            Assert.True(await svc.AssignMailAccountToUserAsync(user.Id, acct.Id));
            Assert.True(await svc.AssignMailAccountToUserAsync(user.Id, acct.Id));
            Assert.Single(await ctx.UserMailAccounts.AsNoTracking()
                .Where(uma => uma.UserId == user.Id && uma.MailAccountId == acct.Id).ToListAsync());
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task RemoveMailAccountFromUserAsync_Assigned_Removes()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var user = await SeedUserAsync(ctx);
            var acct = new MailAccount
            {
                Name = "rm", EmailAddress = $"{Guid.NewGuid():N}@t.local",
                Provider = ProviderType.IMAP, IsEnabled = true, LastSync = DateTime.UtcNow
            };
            ctx.MailAccounts.Add(acct);
            await ctx.SaveChangesAsync();
            ctx.UserMailAccounts.Add(new UserMailAccount { UserId = user.Id, MailAccountId = acct.Id });
            await ctx.SaveChangesAsync();
            var svc = ServiceFactory.CreateUserService(ctx);
            Assert.True(await svc.RemoveMailAccountFromUserAsync(user.Id, acct.Id));
            Assert.False(await ctx.UserMailAccounts.AnyAsync(uma => uma.UserId == user.Id && uma.MailAccountId == acct.Id));
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task RemoveMailAccountFromUserAsync_NotAssigned_ReturnsFalse()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var user = await SeedUserAsync(ctx);
            var acct = new MailAccount
            {
                Name = "rm2", EmailAddress = $"{Guid.NewGuid():N}@t.local",
                Provider = ProviderType.IMAP, IsEnabled = true, LastSync = DateTime.UtcNow
            };
            ctx.MailAccounts.Add(acct);
            await ctx.SaveChangesAsync();
            var svc = ServiceFactory.CreateUserService(ctx);
            Assert.False(await svc.RemoveMailAccountFromUserAsync(user.Id, acct.Id));
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task GetUserMailAccountsAsync_ReturnsAssigned()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var user = await SeedUserAsync(ctx);
            var acct = new MailAccount
            {
                Name = "getlist", EmailAddress = $"{Guid.NewGuid():N}@t.local",
                Provider = ProviderType.IMAP, IsEnabled = true, LastSync = DateTime.UtcNow
            };
            ctx.MailAccounts.Add(acct);
            await ctx.SaveChangesAsync();
            ctx.UserMailAccounts.Add(new UserMailAccount { UserId = user.Id, MailAccountId = acct.Id });
            await ctx.SaveChangesAsync();
            var svc = ServiceFactory.CreateUserService(ctx);
            var accounts = await svc.GetUserMailAccountsAsync(user.Id);
            Assert.Single(accounts);
            Assert.Equal(acct.Id, accounts[0].Id);
        }
        finally { await scope.RollbackAsync(); }
    }

    // ============================================================
    // GetOrCreateUserFromRemoteIdentity
    // ============================================================

    private static ClaimsIdentity BuildOidcIdentity(string remoteId, string email, string? displayName = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, remoteId),
            new(ClaimTypes.Email, email)
        };
        if (displayName != null) claims.Add(new(ClaimTypes.Name, displayName));
        return new ClaimsIdentity(claims, "oidc");
    }

    [Fact]
    public async Task GetOrCreateUserFromRemoteIdentity_NewUser_CreatesWithRemoteId()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var svc = ServiceFactory.CreateUserService(ctx, new OAuthOptions());
            var identity = BuildOidcIdentity("remote-new", "newoidc@test.local", "Alice");
            var user = await svc.GetOrCreateUserFromRemoteIdentity(identity);
            Assert.Equal("remote-new", user.OAuthRemoteUserId);
            Assert.Equal("newoidc@test.local", user.Email);
            Assert.Null(user.PasswordHash);
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task GetOrCreateUserFromRemoteIdentity_ExistingByRemoteId_Returns()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            await SeedUserAsync(ctx, oauthRemoteUserId: "remote-exist");
            var svc = ServiceFactory.CreateUserService(ctx);
            var identity = BuildOidcIdentity("remote-exist", "any@test.local");
            var user = await svc.GetOrCreateUserFromRemoteIdentity(identity);
            Assert.Equal("remote-exist", user.OAuthRemoteUserId);
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task GetOrCreateUserFromRemoteIdentity_EmailCollision_ThrowsInvalidOperationException()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            await SeedUserAsync(ctx);
            var existing = await ctx.Users.AsNoTracking().FirstAsync();
            var svc = ServiceFactory.CreateUserService(ctx);
            var identity = BuildOidcIdentity("remote-other", existing.Email);
            await Assert.ThrowsAsync<InvalidOperationException>(() => svc.GetOrCreateUserFromRemoteIdentity(identity));
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task GetOrCreateUserFromRemoteIdentity_AdminEmail_AutoApprovesAndSetsAdmin()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var oauth = new OAuthOptions { AdminEmails = new[] { "admin@oidc.local" } };
            var svc = ServiceFactory.CreateUserService(ctx, oauth);
            var identity = BuildOidcIdentity("remote-admin", "admin@oidc.local", "Admin");
            var user = await svc.GetOrCreateUserFromRemoteIdentity(identity);
            Assert.True(user.IsAdmin);
            Assert.True(user.IsActive);
            Assert.False(user.RequiresApproval);
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task GetOrCreateUserFromRemoteIdentity_AutoApproveUsers_CreatesActive()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var oauth = new OAuthOptions { AutoApproveUsers = true };
            var svc = ServiceFactory.CreateUserService(ctx, oauth);
            var identity = BuildOidcIdentity("remote-auto", "auto@oidc.local", "Auto");
            var user = await svc.GetOrCreateUserFromRemoteIdentity(identity);
            Assert.True(user.IsActive);
            Assert.False(user.RequiresApproval);
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task GetOrCreateUserFromRemoteIdentity_Default_RequiresApproval()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var svc = ServiceFactory.CreateUserService(ctx, new OAuthOptions());
            var identity = BuildOidcIdentity("remote-pending", "pending@oidc.local", "Pending");
            var user = await svc.GetOrCreateUserFromRemoteIdentity(identity);
            Assert.False(user.IsActive);
            Assert.True(user.RequiresApproval);
        }
        finally { await scope.RollbackAsync(); }
    }

    // ============================================================
    // 2FA
    // ============================================================

    [Fact]
    public async Task SetTwoFactorEnabledAsync_True_SetsFlag()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var user = await SeedUserAsync(ctx);
            var svc = ServiceFactory.CreateUserService(ctx);
            Assert.True(await svc.SetTwoFactorEnabledAsync(user.Id, true));
            Assert.True((await ctx.Users.AsNoTracking().FirstAsync(u => u.Id == user.Id)).IsTwoFactorEnabled);
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task SetTwoFactorEnabledAsync_OidcUser_ReturnsFalse()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var user = await SeedUserAsync(ctx, oauthRemoteUserId: "remote");
            var svc = ServiceFactory.CreateUserService(ctx);
            Assert.False(await svc.SetTwoFactorEnabledAsync(user.Id, true));
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task SetTwoFactorEnabledAsync_Nonexistent_ReturnsFalse()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var svc = ServiceFactory.CreateUserService(ctx);
            Assert.False(await svc.SetTwoFactorEnabledAsync(int.MaxValue - 1, true));
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task SetTwoFactorSecretAsync_Persists()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var user = await SeedUserAsync(ctx);
            var svc = ServiceFactory.CreateUserService(ctx);
            Assert.True(await svc.SetTwoFactorSecretAsync(user.Id, "SECRETBASE32"));
            Assert.Equal("SECRETBASE32", (await ctx.Users.AsNoTracking().FirstAsync(u => u.Id == user.Id)).TwoFactorSecret);
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task SetTwoFactorBackupCodesAsync_HashesCodes()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var user = await SeedUserAsync(ctx);
            var svc = ServiceFactory.CreateUserService(ctx);
            const string codes = "code1;code2;code3";
            Assert.True(await svc.SetTwoFactorBackupCodesAsync(user.Id, codes));
            var stored = (await ctx.Users.AsNoTracking().FirstAsync(u => u.Id == user.Id)).TwoFactorBackupCodes;
            // Stored codes are SHA256-hashed, not plaintext.
            Assert.DoesNotContain("code1", stored);
            Assert.Equal(3, stored.Split(';').Length);
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task GetTwoFactorSecretAsync_ReturnsSecret()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var user = await SeedUserAsync(ctx);
            user.TwoFactorSecret = "TOPSECRET";
            await ctx.SaveChangesAsync();
            var svc = ServiceFactory.CreateUserService(ctx);
            Assert.Equal("TOPSECRET", await svc.GetTwoFactorSecretAsync(user.Id));
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task GetTwoFactorSecretAsync_Nonexistent_ReturnsNull()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var svc = ServiceFactory.CreateUserService(ctx);
            Assert.Null(await svc.GetTwoFactorSecretAsync(int.MaxValue - 1));
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task VerifyTwoFactorBackupCodeAsync_Valid_ReturnsTrue()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var user = await SeedUserAsync(ctx);
            var svc = ServiceFactory.CreateUserService(ctx);
            const string codes = "valid-code;other-code";
            await svc.SetTwoFactorBackupCodesAsync(user.Id, codes);
            Assert.True(await svc.VerifyTwoFactorBackupCodeAsync(user.Id, "valid-code"));
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task VerifyTwoFactorBackupCodeAsync_Invalid_ReturnsFalse()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var user = await SeedUserAsync(ctx);
            var svc = ServiceFactory.CreateUserService(ctx);
            await svc.SetTwoFactorBackupCodesAsync(user.Id, "real-code");
            Assert.False(await svc.VerifyTwoFactorBackupCodeAsync(user.Id, "wrong-code"));
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task VerifyTwoFactorBackupCodeAsync_NoCodes_ReturnsFalse()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var user = await SeedUserAsync(ctx);
            var svc = ServiceFactory.CreateUserService(ctx);
            Assert.False(await svc.VerifyTwoFactorBackupCodeAsync(user.Id, "anything"));
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task RemoveUsedBackupCodeAsync_RemovesAndPersists()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var user = await SeedUserAsync(ctx);
            var svc = ServiceFactory.CreateUserService(ctx);
            await svc.SetTwoFactorBackupCodesAsync(user.Id, "keep;remove;also-keep");
            Assert.True(await svc.RemoveUsedBackupCodeAsync(user.Id, "remove"));
            // "remove" should no longer verify.
            Assert.False(await svc.VerifyTwoFactorBackupCodeAsync(user.Id, "remove"));
            Assert.True(await svc.VerifyTwoFactorBackupCodeAsync(user.Id, "keep"));
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task RemoveUsedBackupCodeAsync_NotPresent_ReturnsFalse()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var user = await SeedUserAsync(ctx);
            var svc = ServiceFactory.CreateUserService(ctx);
            await svc.SetTwoFactorBackupCodesAsync(user.Id, "keep");
            Assert.False(await svc.RemoveUsedBackupCodeAsync(user.Id, "nonexistent"));
        }
        finally { await scope.RollbackAsync(); }
    }
}
using MailArchiver.Models;
using MailArchiver.Services;
using MailArchiver.Tests.Infrastructure;
using Xunit;

namespace MailArchiver.Tests.Services;

/// <summary>
/// Integration tests for <see cref="AccessLogService"/> against the PostgreSQL Dev database.
/// Each test runs inside a rolled-back transaction so no rows persist.
/// </summary>
[Collection(TestDbFixture.CollectionName)]
public class AccessLogServiceTests
{
    private readonly TestDbFixture _fixture;

    public AccessLogServiceTests(TestDbFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task LogAccessAsync_PersistsEntry()
    {
        var username = $"testuser-{Guid.NewGuid():N}";
        await using var scope = await _fixture.CreateTransactionalContextAsync();
            var ctx = scope.Context;
        try
        {
            var svc = new AccessLogService(ctx);
            await svc.LogAccessAsync(username, AccessLogType.Login, emailId: 42, emailSubject: "Subj");

            var logs = await svc.GetLogsForUserAsync(username, limit: 100);
            Assert.Single(logs);
            Assert.Equal(AccessLogType.Login, logs[0].Type);
            Assert.Equal(42, logs[0].EmailId);
            Assert.Equal("Subj", logs[0].EmailSubject);
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task LogAccessAsync_OptionalFields_NullByDefault()
    {
        var username = $"testuser-{Guid.NewGuid():N}";
        await using var scope = await _fixture.CreateTransactionalContextAsync();
            var ctx = scope.Context;
        try
        {
            var svc = new AccessLogService(ctx);
            await svc.LogAccessAsync(username, AccessLogType.Search, searchParameters: "q=foo");

            var logs = await svc.GetLogsForUserAsync(username, limit: 100);
            Assert.Single(logs);
            Assert.Null(logs[0].EmailId);
            Assert.Equal("q=foo", logs[0].SearchParameters);
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task GetLogsForUserAsync_RespectsLimit()
    {
        var username = $"testuser-{Guid.NewGuid():N}";
        await using var scope = await _fixture.CreateTransactionalContextAsync();
            var ctx = scope.Context;
        try
        {
            var svc = new AccessLogService(ctx);
            for (int i = 0; i < 5; i++)
                await svc.LogAccessAsync(username, AccessLogType.Open);

            var logs = await svc.GetLogsForUserAsync(username, limit: 2);
            Assert.Equal(2, logs.Count);
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task GetLogsForUserAsync_OnlyReturnsOwnLogs()
    {
        var userA = $"userA-{Guid.NewGuid():N}";
        var userB = $"userB-{Guid.NewGuid():N}";
        await using var scope = await _fixture.CreateTransactionalContextAsync();
            var ctx = scope.Context;
        try
        {
            var svc = new AccessLogService(ctx);
            await svc.LogAccessAsync(userA, AccessLogType.Login);
            await svc.LogAccessAsync(userB, AccessLogType.Login);

            var logsA = await svc.GetLogsForUserAsync(userA, limit: 100);
            Assert.All(logsA, l => Assert.Equal(userA, l.Username));
            Assert.Single(logsA);
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task GetLogsForUserAsync_DateFilter_Works()
    {
        var username = $"testuser-{Guid.NewGuid():N}";
        await using var scope = await _fixture.CreateTransactionalContextAsync();
            var ctx = scope.Context;
        try
        {
            var svc = new AccessLogService(ctx);
            await svc.LogAccessAsync(username, AccessLogType.Login);

            var from = DateTime.UtcNow.AddMinutes(-5);
            var to = DateTime.UtcNow.AddMinutes(5);
            var logs = await svc.GetLogsForUserAsync(username, from, to);
            Assert.NotEmpty(logs);

            var futureOnly = await svc.GetLogsForUserAsync(username, DateTime.UtcNow.AddYears(1), null);
            Assert.Empty(futureOnly);
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task GetLogsForAdminAsync_ReturnsAllUsersOrderedByTimestampDesc()
    {
        var userA = $"userA-{Guid.NewGuid():N}";
        var userB = $"userB-{Guid.NewGuid():N}";
        await using var scope = await _fixture.CreateTransactionalContextAsync();
            var ctx = scope.Context;
        try
        {
            var svc = new AccessLogService(ctx);
            await svc.LogAccessAsync(userA, AccessLogType.Login);
            await Task.Delay(20);
            await svc.LogAccessAsync(userB, AccessLogType.Login);

            var logs = await svc.GetLogsForAdminAsync(limit: 1000);
            Assert.NotEmpty(logs);
            for (int i = 1; i < logs.Count; i++)
                Assert.True(logs[i - 1].Timestamp >= logs[i].Timestamp);
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task GetLogsForAdminAsync_DateFilter_Works()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
            var ctx = scope.Context;
        try
        {
            var svc = new AccessLogService(ctx);
            var future = await svc.GetLogsForAdminAsync(DateTime.UtcNow.AddYears(1), null);
            Assert.Empty(future);
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task LogAccessAsync_AllAccessLogTypes_RoundTrip()
    {
        var username = $"testuser-{Guid.NewGuid():N}";
        await using var scope = await _fixture.CreateTransactionalContextAsync();
            var ctx = scope.Context;
        try
        {
            var svc = new AccessLogService(ctx);
            foreach (AccessLogType t in Enum.GetValues(typeof(AccessLogType)))
                await svc.LogAccessAsync(username, t);

            var logs = await svc.GetLogsForUserAsync(username, limit: 100);
            Assert.Equal(Enum.GetValues(typeof(AccessLogType)).Length, logs.Count);
        }
        finally { await scope.RollbackAsync(); }
    }
}
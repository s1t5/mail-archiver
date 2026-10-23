using MailArchiver.Services;
using Xunit;

namespace MailArchiver.Tests.Services;

/// <summary>
/// Only the job's owner (or an admin) may cancel a background job. Job models carry the
/// creating user's display name in UserId; "System" marks operator/CLI-originated jobs
/// reserved for admins (P2).
/// </summary>
public class JobOwnershipTests
{
    [Theory]
    [InlineData("alice", true,  "bob",    true)]   // Admin darf alles
    [InlineData("alice", false, "alice",  true)]   // Owner darf eigene
    [InlineData("alice", false, "Alice",  true)]   // case-insensitiv (Anzeigename)
    [InlineData("alice", false, "bob",    false)]  // Fremde: deny
    [InlineData("alice", false, "System", false)]  // Operator/CLI-Job: nur Admin
    [InlineData("alice", false, null,     false)]
    [InlineData(null,    false, "bob",    false)]
    [InlineData(null,    true,  null,     true)]   // Admin auch bei unbekanntem Job-Owner
    public void MayCancel_ImplementsOwnerOrAdmin(string? acting, bool isAdmin, string? jobOwner, bool expected)
        => Assert.Equal(expected, JobOwnership.MayCancel(acting, isAdmin, jobOwner));
}
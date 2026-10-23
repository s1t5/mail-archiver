namespace MailArchiver.Services;

/// <summary>
/// Decides whether the acting user may cancel a background job: admins may cancel
/// anything, everyone else only jobs they created themselves. Job models carry the
/// creating user's display name in <c>UserId</c>; "System"/"CLI" mark operator-originated
/// jobs reserved for admins (P2).
/// </summary>
public static class JobOwnership
{
    public static bool MayCancel(string? actingUser, bool isAdmin, string? jobUserId)
        => isAdmin
           || (!string.IsNullOrEmpty(actingUser)
               && string.Equals(actingUser, jobUserId, StringComparison.OrdinalIgnoreCase));
}
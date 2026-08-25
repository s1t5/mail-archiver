// Services/Shared/OffloadTargetEligibility.cs
using MailArchiver.Models;

namespace MailArchiver.Services.Shared
{
    /// <summary>
    /// Why a mailbox cannot be offloaded into. <see cref="NotFound"/> and
    /// <see cref="NotAccessible"/> are kept apart so the log can say which one it was, but the
    /// form deliberately renders the same message for both: telling a user that an account
    /// exists but is not theirs would let them enumerate account IDs.
    /// </summary>
    public enum OffloadTargetRejection
    {
        None,
        SameAsSource,
        NotFound,
        NotAccessible,
        NotImap,
        Disabled,
    }

    /// <summary>The facts about a candidate target that decide whether it may be used.</summary>
    public sealed class OffloadTargetCandidate
    {
        public int Id { get; init; }
        public ProviderType Provider { get; init; }
        public bool IsEnabled { get; init; }

        /// <summary>
        /// Whether the acting user may use this account, as resolved by
        /// <c>IAccountAccessResolver</c>. Passed in rather than computed here so this stays a
        /// pure decision over facts and can be tested without a database or an HTTP context.
        /// </summary>
        public bool IsAccessible { get; init; }
    }

    /// <summary>
    /// The single place that decides whether an account may be an offload target. Both the
    /// target dropdown and the POST that starts a job route their decision through here, so the
    /// list a user is shown and the list the server will accept cannot drift apart. The list is
    /// only a convenience; <see cref="Evaluate"/> on the POST is the boundary.
    /// </summary>
    public static class OffloadTargetEligibility
    {
        /// <summary>
        /// Applies the scope that <c>IAccountAccessResolver</c> returns: null means admin, i.e.
        /// every account, and an empty list means no account at all. Fails closed by treating
        /// anything not in a non-null list as inaccessible.
        /// </summary>
        public static bool IsAccessible(int accountId, IReadOnlyCollection<int>? allowedAccountIds)
            => allowedAccountIds == null || allowedAccountIds.Contains(accountId);

        /// <summary>
        /// Order matters. Accessibility is decided before the provider and the enabled flag,
        /// because a message naming either of those about an account the user may not see would
        /// disclose something about it.
        /// </summary>
        public static OffloadTargetRejection Evaluate(OffloadTargetCandidate? target, int sourceAccountId)
        {
            if (target == null) return OffloadTargetRejection.NotFound;
            if (target.Id == sourceAccountId) return OffloadTargetRejection.SameAsSource;
            if (!target.IsAccessible) return OffloadTargetRejection.NotAccessible;
            if (target.Provider != ProviderType.IMAP) return OffloadTargetRejection.NotImap;
            if (!target.IsEnabled) return OffloadTargetRejection.Disabled;
            return OffloadTargetRejection.None;
        }

        public static bool IsEligible(OffloadTargetCandidate? target, int sourceAccountId)
            => Evaluate(target, sourceAccountId) == OffloadTargetRejection.None;

        /// <summary>
        /// Resource key for the message shown on the form. NotFound and NotAccessible share one
        /// on purpose; see <see cref="OffloadTargetRejection"/>.
        /// </summary>
        public static string MessageKey(OffloadTargetRejection rejection) => rejection switch
        {
            OffloadTargetRejection.SameAsSource => "OffloadTargetMustDiffer",
            OffloadTargetRejection.NotFound => "OffloadTargetNotFound",
            OffloadTargetRejection.NotAccessible => "OffloadTargetNotFound",
            OffloadTargetRejection.NotImap => "OffloadTargetMustBeImap",
            OffloadTargetRejection.Disabled => "OffloadTargetDisabled",
            _ => throw new ArgumentOutOfRangeException(nameof(rejection), rejection, "No message for an accepted target."),
        };
    }
}

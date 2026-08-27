using MailArchiver.Models;
using MailArchiver.Services.Shared;

namespace MailArchiver.Tests.Services;

/// <summary>
/// The offload target is the one end of the operation the acting user does not necessarily own,
/// so these are the rules that keep a self-manager from appending into somebody else's mailbox.
/// The order of the checks is part of the contract: a rejection message must never say anything
/// about an account the user may not see.
/// </summary>
public class OffloadTargetEligibilityTests
{
    private const int SourceId = 1;

    private static OffloadTargetCandidate Candidate(
        int id = 2,
        ProviderType provider = ProviderType.IMAP,
        bool isEnabled = true,
        bool isAccessible = true)
        => new() { Id = id, Provider = provider, IsEnabled = isEnabled, IsAccessible = isAccessible };

    // --- IsAccessible: the scope IAccountAccessResolver hands over -------------------------

    [Fact]
    public void IsAccessible_NullScope_IsAdminAndAllowsEverything()
    {
        Assert.True(OffloadTargetEligibility.IsAccessible(1, null));
        Assert.True(OffloadTargetEligibility.IsAccessible(9999, null));
    }

    [Fact]
    public void IsAccessible_EmptyScope_AllowsNothing()
    {
        Assert.False(OffloadTargetEligibility.IsAccessible(1, new List<int>()));
    }

    [Fact]
    public void IsAccessible_OnlyTheAssignedAccounts()
    {
        var allowed = new List<int> { 2, 5 };
        Assert.True(OffloadTargetEligibility.IsAccessible(2, allowed));
        Assert.True(OffloadTargetEligibility.IsAccessible(5, allowed));
        Assert.False(OffloadTargetEligibility.IsAccessible(3, allowed));
    }

    // --- Evaluate -------------------------------------------------------------------------

    [Fact]
    public void Evaluate_AnAssignedEnabledImapAccount_IsAccepted()
    {
        Assert.Equal(OffloadTargetRejection.None, OffloadTargetEligibility.Evaluate(Candidate(), SourceId));
        Assert.True(OffloadTargetEligibility.IsEligible(Candidate(), SourceId));
    }

    [Fact]
    public void Evaluate_MissingAccount_IsNotFound()
    {
        Assert.Equal(OffloadTargetRejection.NotFound, OffloadTargetEligibility.Evaluate(null, SourceId));
        Assert.False(OffloadTargetEligibility.IsEligible(null, SourceId));
    }

    [Fact]
    public void Evaluate_TheSourceItself_IsRejectedEvenWhenOtherwisePerfect()
    {
        var self = Candidate(id: SourceId);
        Assert.Equal(OffloadTargetRejection.SameAsSource, OffloadTargetEligibility.Evaluate(self, SourceId));
    }

    [Fact]
    public void Evaluate_AnAccountTheUserIsNotAssignedTo_IsRejected()
    {
        var foreign = Candidate(isAccessible: false);
        Assert.Equal(OffloadTargetRejection.NotAccessible, OffloadTargetEligibility.Evaluate(foreign, SourceId));
    }

    [Fact]
    public void Evaluate_AGraphAccount_IsRejected()
    {
        var graph = Candidate(provider: ProviderType.M365);
        Assert.Equal(OffloadTargetRejection.NotImap, OffloadTargetEligibility.Evaluate(graph, SourceId));
    }

    [Fact]
    public void Evaluate_ADisabledAccount_IsRejected()
    {
        var disabled = Candidate(isEnabled: false);
        Assert.Equal(OffloadTargetRejection.Disabled, OffloadTargetEligibility.Evaluate(disabled, SourceId));
    }

    /// <summary>
    /// Accessibility is checked before the provider and the enabled flag. Otherwise the form
    /// would answer "that is not an IMAP account" or "that account is disabled" about a mailbox
    /// the user is not allowed to know anything about.
    /// </summary>
    [Theory]
    [InlineData(ProviderType.M365, true)]
    [InlineData(ProviderType.IMAP, false)]
    [InlineData(ProviderType.M365, false)]
    public void Evaluate_InaccessibleWins_OverProviderAndEnabledState(ProviderType provider, bool isEnabled)
    {
        var candidate = Candidate(provider: provider, isEnabled: isEnabled, isAccessible: false);
        Assert.Equal(OffloadTargetRejection.NotAccessible, OffloadTargetEligibility.Evaluate(candidate, SourceId));
    }

    /// <summary>
    /// The source is rejected before accessibility is consulted, which is harmless: the user
    /// already has access to the source, or the page would not have opened.
    /// </summary>
    [Fact]
    public void Evaluate_SameAsSourceWins_OverEverythingElse()
    {
        var self = Candidate(id: SourceId, provider: ProviderType.M365, isEnabled: false, isAccessible: false);
        Assert.Equal(OffloadTargetRejection.SameAsSource, OffloadTargetEligibility.Evaluate(self, SourceId));
    }

    // --- Messages -------------------------------------------------------------------------

    /// <summary>
    /// "Does not exist" and "is not yours" have to be indistinguishable on the form, or a post
    /// with a guessed ID becomes an account enumeration oracle.
    /// </summary>
    [Fact]
    public void MessageKey_NotFoundAndNotAccessible_AreIndistinguishable()
    {
        Assert.Equal(
            OffloadTargetEligibility.MessageKey(OffloadTargetRejection.NotFound),
            OffloadTargetEligibility.MessageKey(OffloadTargetRejection.NotAccessible));
    }

    [Theory]
    [InlineData(OffloadTargetRejection.SameAsSource, "OffloadTargetMustDiffer")]
    [InlineData(OffloadTargetRejection.NotFound, "OffloadTargetNotFound")]
    [InlineData(OffloadTargetRejection.NotAccessible, "OffloadTargetNotFound")]
    [InlineData(OffloadTargetRejection.NotImap, "OffloadTargetMustBeImap")]
    [InlineData(OffloadTargetRejection.Disabled, "OffloadTargetDisabled")]
    public void MessageKey_MapsEveryRejection(OffloadTargetRejection rejection, string expected)
    {
        Assert.Equal(expected, OffloadTargetEligibility.MessageKey(rejection));
    }

    [Fact]
    public void MessageKey_None_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => OffloadTargetEligibility.MessageKey(OffloadTargetRejection.None));
    }

    /// <summary>
    /// Every rejection the enum can produce needs a message, so adding one cannot silently ship
    /// a form that throws when it tries to explain itself.
    /// </summary>
    [Fact]
    public void MessageKey_CoversEveryRejectionValue()
    {
        foreach (var rejection in Enum.GetValues<OffloadTargetRejection>())
        {
            if (rejection == OffloadTargetRejection.None) continue;
            Assert.False(string.IsNullOrEmpty(OffloadTargetEligibility.MessageKey(rejection)));
        }
    }

    // --- The list and the boundary agree --------------------------------------------------

    /// <summary>
    /// The dropdown is built by filtering candidates through IsEligible and the post decides
    /// with Evaluate. This is the property that matters: anything the list offers is accepted,
    /// and anything it withholds is refused.
    /// </summary>
    [Fact]
    public void TheOfferedListAndTheAcceptedPost_AgreeForASelfManager()
    {
        var allowed = new List<int> { 1, 2 };
        var all = new[]
        {
            Candidate(id: 1),                                                       // the source
            Candidate(id: 2),                                                       // assigned, usable
            Candidate(id: 3, isAccessible: OffloadTargetEligibility.IsAccessible(3, allowed)),
            Candidate(id: 4, isEnabled: false, isAccessible: OffloadTargetEligibility.IsAccessible(4, allowed)),
        };

        var offered = all.Where(c => OffloadTargetEligibility.IsEligible(c, SourceId)).Select(c => c.Id).ToList();
        Assert.Equal(new[] { 2 }, offered);

        foreach (var candidate in all)
        {
            var accepted = OffloadTargetEligibility.Evaluate(candidate, SourceId) == OffloadTargetRejection.None;
            Assert.Equal(offered.Contains(candidate.Id), accepted);
        }
    }
}

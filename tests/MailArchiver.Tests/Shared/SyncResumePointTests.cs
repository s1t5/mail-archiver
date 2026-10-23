using MailArchiver.Services.Shared;

namespace MailArchiver.Tests.Shared;

/// <summary>
/// The resume watermark decides which messages a restarted folder sync is allowed to skip, so every
/// case that returns something other than "read the folder in full" has to be justified.
///
/// The previous mechanism keyed off the Date header of the last archived message and fed it into a
/// search the server answers by INTERNALDATE — a different clock — while messages are processed in
/// UID order rather than date order. Both mismatches could move the search window past mail that had
/// never been archived. These tests pin the replacement down, and in particular that every doubtful
/// case falls back to 0: re-reading a folder costs time and is caught by the duplicate check,
/// skipping one loses mail silently.
/// </summary>
public class SyncResumePointTests
{
    private const uint FolderUidValidity = 4711;

    private static uint Resolve(long? lastUid, long? uidValidity, uint folderUidValidity = FolderUidValidity)
        => SyncResumePoint.ResolveAfterUid(lastUid, uidValidity, folderUidValidity);

    // ---- the one case that resumes ------------------------------------------------------------

    [Fact]
    public void A_matching_checkpoint_resumes_after_its_uid()
    {
        Assert.Equal(9000u, Resolve(9000, FolderUidValidity));
    }

    [Fact]
    public void The_highest_possible_uid_is_still_a_valid_watermark()
    {
        Assert.Equal(uint.MaxValue, Resolve(uint.MaxValue, FolderUidValidity));
    }

    // ---- no usable watermark ------------------------------------------------------------------

    [Fact]
    public void No_checkpoint_reads_the_folder_in_full()
    {
        Assert.Equal(0u, Resolve(null, null));
    }

    [Fact]
    public void A_checkpoint_written_before_uids_were_recorded_reads_the_folder_in_full()
    {
        // Rows that predate the LastUid column, and rows GetOrCreateCheckpointAsync created before
        // the first message of a folder was archived.
        Assert.Equal(0u, Resolve(null, FolderUidValidity));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_non_positive_uid_is_not_a_watermark(long lastUid)
    {
        Assert.Equal(0u, Resolve(lastUid, FolderUidValidity));
    }

    // ---- UIDVALIDITY guards -------------------------------------------------------------------

    [Fact]
    public void A_renumbered_folder_invalidates_the_checkpoint()
    {
        // The server handed out a new UIDVALIDITY, so UID 9000 now names a different message than
        // the one that was archived. Anything but a full read would skip real mail.
        Assert.Equal(0u, Resolve(9000, 1234));
    }

    [Fact]
    public void A_checkpoint_without_a_recorded_uidvalidity_is_not_trusted()
    {
        // Cannot be proven to belong to this incarnation of the folder, so it does not count.
        Assert.Equal(0u, Resolve(9000, null));
    }

    // ---- corruption ---------------------------------------------------------------------------

    [Fact]
    public void A_uid_beyond_the_32_bit_range_is_rejected()
    {
        Assert.Equal(0u, Resolve((long)uint.MaxValue + 1, FolderUidValidity));
    }
}

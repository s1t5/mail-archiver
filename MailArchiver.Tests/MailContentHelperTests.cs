using MailArchiver.Services.Shared;
using Xunit;

namespace MailArchiver.Tests;

// has:attachment must count only REAL file attachments. A CID-less inline part
// (Content-Disposition: inline, no Content-ID) must receive a synthetic ContentId so it is
// treated as inline and excluded (P2, Codex 2a1c0db).
public class MailContentHelperTests
{
    [Fact]
    public void Real_attachment_without_disposition_keeps_null()
        => Assert.Null(MailContentHelper.ResolveAttachmentContentId(null, null));

    [Fact]
    public void Real_attachment_disposition_attachment_keeps_null()
        => Assert.Null(MailContentHelper.ResolveAttachmentContentId(null, "attachment"));

    [Fact]
    public void Cid_less_inline_gets_synthetic_marker()
        => Assert.False(string.IsNullOrEmpty(MailContentHelper.ResolveAttachmentContentId(null, "inline")));

    [Fact]
    public void Cid_less_inline_is_case_insensitive()
        => Assert.False(string.IsNullOrEmpty(MailContentHelper.ResolveAttachmentContentId(null, "Inline")));

    [Fact]
    public void Existing_contentid_is_preserved()
        => Assert.Equal("logo@sig", MailContentHelper.ResolveAttachmentContentId("logo@sig", "inline"));
}

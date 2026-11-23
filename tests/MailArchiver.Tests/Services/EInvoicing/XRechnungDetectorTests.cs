using MailArchiver.Services.EInvoicing;

namespace MailArchiver.Tests.Services.EInvoicing;

public class XRechnungDetectorTests
{
    private static byte[] Fixture(string name)
        => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "EInvoicing", name));

    [Fact]
    public void Detect_Ubl_ReturnsXRechnungInfo()
    {
        var info = XRechnungDetector.Detect(Fixture("xrechnung-ubl.xml"));

        Assert.NotNull(info);
        Assert.Equal("UBL", info!.Format);
        Assert.Equal("XRechnung 3.0", info.Standard);
        Assert.Equal("Standard", info.Profile);
    }

    [Fact]
    public void Detect_Cii_ReturnsXRechnungInfo()
    {
        var info = XRechnungDetector.Detect(Fixture("xrechnung-cii.xml"));

        Assert.NotNull(info);
        Assert.Equal("CII", info!.Format);
        Assert.Equal("XRechnung 3.0", info.Standard);
    }

    [Fact]
    public void Detect_CreditNote_ReturnsXRechnungInfo()
    {
        var info = XRechnungDetector.Detect(Fixture("xrechnung-creditnote.xml"));

        Assert.NotNull(info);
        Assert.Equal("UBL", info!.Format);
    }

    [Fact]
    public void Detect_PeppolWithoutXRechnung_ReturnsNull()
        => Assert.Null(XRechnungDetector.Detect(Fixture("ubl-peppol.xml")));

    [Fact]
    public void Detect_BrokenXml_ReturnsNull()
        => Assert.Null(XRechnungDetector.Detect("<Invoice><broken>"u8.ToArray()));

    [Fact]
    public void Detect_TooLarge_ReturnsNull()
        => Assert.Null(XRechnungDetector.Detect(new byte[XRechnungDetector.MaxBytes + 1]));

    [Theory]
    [InlineData("rechnung.xml", null, true)]
    [InlineData("rechnung.XML", null, true)]
    [InlineData("rechnung.pdf", "application/xml", true)]
    [InlineData("rechnung.pdf", "text/xml; charset=utf-8", true)]
    [InlineData("rechnung.pdf", "application/pdf", false)]
    [InlineData(null, null, false)]
    public void LooksLikeXml_ChecksNameAndContentType(string? name, string? contentType, bool expected)
        => Assert.Equal(expected, XRechnungDetector.LooksLikeXml(name, contentType));
}

using System.Xml;
using System.Xml.Linq;
using MailArchiver.Models;
using MailArchiver.Services;
using MailArchiver.Utilities;
using Microsoft.Extensions.Options;

namespace MailArchiver.Tests.Services;

public class AuditExportServiceStaticTests
{
    private static DateTimeHelper CreateDateTimeHelper(string timeZoneId = "Europe/Berlin")
        => new(Options.Create(new TimeZoneOptions { DisplayTimeZoneId = timeZoneId }));

    [Fact]
    public void Csv_PlainValue_IsNotQuoted()
    {
        Assert.Equal("hello", AuditExportService.Csv("hello"));
    }

    [Fact]
    public void Csv_EmptyValue_YieldsEmptyQuotedField()
    {
        Assert.Equal("\"\"", AuditExportService.Csv(string.Empty));
        Assert.Equal("\"\"", AuditExportService.Csv(null!));
    }

    [Theory]
    [InlineData("a;b", "\"a;b\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("line\nbreak", "\"line\nbreak\"")]
    [InlineData("line\rbreak", "\"line\rbreak\"")]
    public void Csv_SpecialCharacters_AreQuotedAndEscaped(string input, string expected)
    {
        Assert.Equal(expected, AuditExportService.Csv(input));
    }

    [Fact]
    public void Csv_Umlauts_ArePreserved()
    {
        Assert.Equal("Grüße aus München", AuditExportService.Csv("Grüße aus München"));
    }

    [Fact]
    public void ToIso8601Utc_ConvertsDisplayTimeZoneToUtc()
    {
        // 12:00 wall-clock in Europe/Berlin (summer time, UTC+2) is 10:00 UTC
        var helper = CreateDateTimeHelper("Europe/Berlin");
        var display = new DateTime(2024, 7, 1, 12, 0, 0, DateTimeKind.Unspecified);
        Assert.Equal("2024-07-01T10:00:00Z", AuditExportService.ToIso8601Utc(display, helper));
    }

    [Fact]
    public void ToIso8601Utc_WinterTime()
    {
        var helper = CreateDateTimeHelper("Europe/Berlin");
        var display = new DateTime(2024, 1, 15, 3, 30, 0, DateTimeKind.Unspecified);
        Assert.Equal("2024-01-15T02:30:00Z", AuditExportService.ToIso8601Utc(display, helper));
    }

    [Fact]
    public void ToIso8601Utc_UtcTimeZone_PassThrough()
    {
        var helper = CreateDateTimeHelper("Etc/UCT");
        var display = new DateTime(2024, 7, 1, 12, 0, 0, DateTimeKind.Unspecified);
        Assert.Equal("2024-07-01T12:00:00Z", AuditExportService.ToIso8601Utc(display, helper));
    }

    /// <summary>
    /// A representative INDEX.XML (as emitted by the export service writer code) must
    /// validate against the embedded DTD: element order per the Table/VariableLength
    /// content models, DOCTYPE referencing the shipped DTD file name.
    /// </summary>
    [Fact]
    public void IndexXml_ValidatedAgainstEmbeddedDtd()
    {
        Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "Resources"));
        var dtdPath = Path.Combine(AppContext.BaseDirectory, "Resources", "index.dtd");
        if (!File.Exists(dtdPath))
        {
            // Not present as file in test output - extract from the main assembly
            var assembly = typeof(AuditExportService).Assembly;
            var resourceName = assembly.GetManifestResourceNames()
                .First(n => n.EndsWith("index.dtd", StringComparison.OrdinalIgnoreCase));
            using var stream = assembly.GetManifestResourceStream(resourceName)!;
            using var fs = new FileStream(dtdPath, FileMode.Create, FileAccess.Write);
            stream.CopyTo(fs);
        }

        var xml = BuildSampleIndexXml();

        // Write the sample next to a copy of the DTD so the DOCTYPE system id resolves
        var workDir = Path.Combine(Path.GetTempPath(), "audit-dtd-test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        var dtdCopy = Path.Combine(workDir, "index.dtd");
        File.Copy(dtdPath, dtdCopy);
        var xmlPath = Path.Combine(workDir, "INDEX.XML");
        File.WriteAllText(xmlPath, xml, new System.Text.UTF8Encoding(false));

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Parse,
            XmlResolver = new XmlUrlResolver(),
            ValidationType = ValidationType.None
        };
        using (var reader = XmlReader.Create(xmlPath, settings))
        {
            // Consume the reader fully; DTD well-formedness/content-model errors throw XmlException.
            while (reader.Read()) { }
        }
        try
        {
            Directory.Delete(workDir, true);
        }
        catch
        {
            // best effort
        }

        // Additional structural assertions
        var doc = XDocument.Parse(xml);
        Assert.Equal("DataSet", doc.Root!.Name.LocalName);
        Assert.Equal("index.dtd", doc.Document!.DocumentType!.SystemId);
        Assert.NotNull(doc.Root!.Element("DataSupplier")!.Element("Name"));
        var tables = doc.Root!.Element("Media")!.Elements("Table").ToList();
        Assert.Equal(2, tables.Count);
        Assert.Equal("emails.csv", tables[0].Element("URL")!.Value);
        Assert.Equal("attachments.csv", tables[1].Element("URL")!.Value);

        var columns = tables[0].Element("VariableLength")!.Elements("VariableColumn").ToList();
        Assert.Equal(2, columns.Count);
        Assert.Equal("Id", columns[0].Element("Name")!.Value);
        Assert.NotNull(columns[0].Element("Numeric"));
        Assert.NotNull(columns[1].Element("AlphaNumeric"));
    }

    private static string BuildSampleIndexXml()
    {
        var settings = new XmlWriterSettings { Indent = true, NewLineChars = "\n", Encoding = new System.Text.UTF8Encoding(false) };
        using var ms = new MemoryStream();
        using (var writer = XmlWriter.Create(ms, settings))
        {
            writer.WriteDocType("DataSet", null, "index.dtd", null);
            writer.WriteStartElement("DataSet");
            writer.WriteElementString("Version", "1.0");
            writer.WriteStartElement("DataSupplier");
            writer.WriteElementString("Name", "ACME GmbH");
            writer.WriteElementString("Location", "Musterstadt");
            writer.WriteElementString("Comment", "E-Mail-Archiv Mail-Archiver");
            writer.WriteEndElement();
            writer.WriteStartElement("Media");
            writer.WriteElementString("Name", "E-Mail-Archiv");

            writer.WriteStartElement("Table");
            writer.WriteElementString("URL", "emails.csv");
            writer.WriteElementString("Name", "E-Mail-Metadaten");
            writer.WriteElementString("Description", "Metadaten archivierter E-Mails");
            writer.WriteElementString("UTF8", null);
            writer.WriteStartElement("VariableLength");
            writer.WriteElementString("ColumnDelimiter", ";");
            writer.WriteStartElement("RecordDelimiter");
            writer.WriteCharEntity('\n');
            writer.WriteEndElement();
            writer.WriteElementString("TextEncapsulator", "\"");
            writer.WriteStartElement("VariableColumn");
            writer.WriteElementString("Name", "Id");
            writer.WriteElementString("Numeric", null);
            writer.WriteEndElement();
            writer.WriteStartElement("VariableColumn");
            writer.WriteElementString("Name", "MessageId");
            writer.WriteElementString("AlphaNumeric", null);
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteStartElement("Table");
            writer.WriteElementString("URL", "attachments.csv");
            writer.WriteElementString("Name", "Anhang-Metadaten");
            writer.WriteElementString("Description", "Metadaten archivierter E-Mail-Anhänge");
            writer.WriteElementString("UTF8", null);
            writer.WriteStartElement("VariableLength");
            writer.WriteElementString("ColumnDelimiter", ";");
            writer.WriteStartElement("RecordDelimiter");
            writer.WriteCharEntity('\n');
            writer.WriteEndElement();
            writer.WriteElementString("TextEncapsulator", "\"");
            writer.WriteStartElement("VariableColumn");
            writer.WriteElementString("Name", "EmailId");
            writer.WriteElementString("Numeric", null);
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();

            writer.WriteEndElement(); // Media
            writer.WriteEndElement(); // DataSet
        }
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }
}
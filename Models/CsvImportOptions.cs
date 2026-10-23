namespace MailArchiver.Models
{
    public class CsvImportOptions
    {
        public const string CsvImport = "CsvImport";
        public int MaxRows { get; set; } = 5000;
        public long MaxFileSizeBytes { get; set; } = 10_000_000;
    }
}
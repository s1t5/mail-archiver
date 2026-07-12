namespace MailArchiver.Models.ViewModels
{
    public class CsvImportResultViewModel
    {
        public int CreatedCount { get; set; }
        public int SkippedCount { get; set; }
        public int FailedCount { get; set; }

        public List<CsvImportCreatedRow> CreatedRows { get; set; } = new();
        public List<CsvImportSkippedRow> SkippedRows { get; set; } = new();
        public List<CsvImportFailedRow> FailedRows { get; set; } = new();
    }

    public class CsvImportCreatedRow
    {
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class CsvImportSkippedRow
    {
        public string Email { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }

    public class CsvImportFailedRow
    {
        public int LineNumber { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }

    internal class CsvParsedRow
    {
        public int LineNumber { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? Username { get; set; }
        public string? ImapServer { get; set; }
        public int? ImapPort { get; set; }
        public bool? UseSSL { get; set; }
    }
}
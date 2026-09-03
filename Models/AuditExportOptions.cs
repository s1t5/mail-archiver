namespace MailArchiver.Models
{
    public class AuditExportOptions
    {
        public const string SectionName = "AuditExport";
        public string DataSupplierName { get; set; } = string.Empty;
        public string DataSupplierLocation { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        public string OutputDirectory { get; set; } = "exports/audit";
        public int RetentionDays { get; set; } = 30;
        public int MaxRangeYears { get; set; } = 10;
    }
}
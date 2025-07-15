namespace MailArchiver.Models.ViewModels
{
    public class ExportViewModel
    {
        public string? SearchTerm { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int? SelectedAccountId { get; set; }
        public bool? IsOutgoing { get; set; }
        public ExportFormat Format { get; set; }
        public int? EmailId { get; set; }
    }

    public enum ExportFormat
    {
        Csv,
        Json,
        Eml
    }
}
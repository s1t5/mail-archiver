namespace MailArchiver.Models
{
    public class BatchOperationOptions
    {
        public const string BatchOperation = "BatchOperation";
        
        public int BatchSize { get; set; } = 50;
        public int PauseBetweenEmailsMs { get; set; } = 50;
        public int PauseBetweenBatchesMs { get; set; } = 250;
    }
}

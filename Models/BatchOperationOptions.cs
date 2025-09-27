namespace MailArchiver.Models
{
    public class BatchOperationOptions
    {
        public const string BatchOperation = "BatchOperation";
        
        public int BatchSize { get; set; } = 20;
        public int PauseBetweenEmailsMs { get; set; } = 100;
        public int PauseBetweenBatchesMs { get; set; } = 500;
    }
}

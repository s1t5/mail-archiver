namespace MailArchiver.Models
{
    public class UploadOptions
    {
        public const string Upload = "Upload";
        
        public int MaxFileSizeGB { get; set; } = 10;
        public int KeepAliveTimeoutHours { get; set; } = 4;
        public int RequestHeadersTimeoutHours { get; set; } = 2;
        public string Notes { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets the maximum file size in bytes
        /// </summary>
        public long MaxFileSizeBytes => MaxFileSizeGB * 1024L * 1024L * 1024L;
        
        /// <summary>
        /// Gets the formatted file size string
        /// </summary>
        public string MaxFileSizeFormatted => $"{MaxFileSizeGB} GB";
    }
}

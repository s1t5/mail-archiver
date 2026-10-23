namespace MailArchiver.Models
{
    public class LocalImportOptions
    {
        public const string LocalImport = "LocalImport";
        public List<string> AllowedPaths { get; set; } = new();
    }
}
using System.ComponentModel.DataAnnotations;

namespace MailArchiver.Models.ViewModels
{
    public class CreateApiKeyViewModel
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// When true the key also carries the <c>write</c> scope and may create,
        /// update and delete data (as far as the deployment's Api settings allow).
        /// Default: false — a key is read-only unless write access is requested.
        /// </summary>
        public bool WriteAccess { get; set; }
    }
}

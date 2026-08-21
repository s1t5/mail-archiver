using System.ComponentModel.DataAnnotations;

namespace MailArchiver.Models.ViewModels
{
    /// <summary>
    /// Form behind the date-windowed offload on the account page. Replaces the plain
    /// "copy all emails" post, which had no date filter and was therefore all or nothing.
    /// </summary>
    public class OffloadViewModel
    {
        public int SourceAccountId { get; set; }
        public string SourceAccountName { get; set; } = string.Empty;

        /// <summary>How much mail the source account holds in total, for context on the form.</summary>
        public int SourceTotalEmails { get; set; }

        [Display(Name = "TargetAccount")]
        [Required(ErrorMessage = "OffloadTargetRequired")]
        public int TargetAccountId { get; set; }

        [Display(Name = "TargetFolder")]
        public string TargetFolder { get; set; } = "INBOX";

        [Display(Name = "PreserveFolderStructure")]
        public bool PreserveFolderStructure { get; set; } = true;

        /// <summary>
        /// A relative window in months, offered because that is how a migration cutoff is
        /// usually expressed. Zero means "use the explicit date below instead".
        /// </summary>
        [Display(Name = "OffloadWindowMonths")]
        [Range(0, 600)]
        public int WindowMonths { get; set; } = 12;

        /// <summary>
        /// Explicit lower bound. Whatever is chosen, the job stores an absolute date, so a
        /// relative window cannot drift between a run and a repeat of it.
        /// </summary>
        [Display(Name = "OffloadCutoffFrom")]
        [DataType(DataType.Date)]
        public DateTime? CutoffFrom { get; set; }

        [Display(Name = "OffloadCutoffTo")]
        [DataType(DataType.Date)]
        public DateTime? CutoffTo { get; set; }

        [Display(Name = "OffloadDryRun")]
        public bool DryRun { get; set; } = true;

        [Display(Name = "OffloadMarkAsSeen")]
        public bool MarkAsSeen { get; set; } = true;

        /// <summary>Accounts that can be offloaded into, i.e. enabled IMAP accounts.</summary>
        public List<TargetAccountOption> AvailableTargets { get; set; } = new();

        /// <summary>Configured exclusions, shown read-only so a run is never a surprise.</summary>
        public List<string> ExcludedSourceFolders { get; set; } = new();

        /// <summary>Configured rename map, shown read-only for the same reason.</summary>
        public Dictionary<string, string> FolderRenameMap { get; set; } = new();

        public class TargetAccountOption
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string EmailAddress { get; set; } = string.Empty;
        }
    }
}

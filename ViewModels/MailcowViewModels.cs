using System.ComponentModel.DataAnnotations;
using MailArchiver.Models;
using MailArchiver.Services.Mailcow;

namespace MailArchiver.ViewModels;

public class CreateMailcowInstanceViewModel
{
    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;
    [Required, StringLength(2048), Display(Name = "Mailcow URL")]
    public string ApiUrl { get; set; } = string.Empty;
    [Required, StringLength(1024), DataType(DataType.Password), Display(Name = "API key")]
    public string ApiKey { get; set; } = string.Empty;
    [Required, StringLength(253), Display(Name = "IMAP host")]
    public string ImapHost { get; set; } = string.Empty;
    [Range(1, 65535), Display(Name = "IMAP TLS port")]
    public int ImapPort { get; set; } = 993;
}

public record MailcowMailboxSelection(MailcowRemoteMailbox Mailbox, MailcowMailbox? Binding, bool AlreadyExists);
public class MailcowMailboxesViewModel
{
    public int InstanceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<MailcowMailboxSelection> Mailboxes { get; set; } = [];
    public List<MailcowMailbox> OrphanedBindings { get; set; } = [];
}

using MailArchiver.Models;
using Microsoft.EntityFrameworkCore;

namespace MailArchiver.Data
{
    public class MailArchiverDbContext : DbContext
    {
        public DbSet<MailAccount> MailAccounts { get; set; }
        public DbSet<ArchivedEmail> ArchivedEmails { get; set; }
        public DbSet<EmailAttachment> EmailAttachments { get; set; }

        public MailArchiverDbContext(DbContextOptions<MailArchiverDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Schema für PostgreSQL definieren
            modelBuilder.HasDefaultSchema("mail_archiver");

            // Verwenden Sie Text anstelle von varchar für unbegrenzte Länge
            modelBuilder.Entity<ArchivedEmail>()
                .Property(e => e.Subject)
                .HasColumnType("text");

            modelBuilder.Entity<ArchivedEmail>()
                .Property(e => e.From)
                .HasColumnType("text");

            modelBuilder.Entity<ArchivedEmail>()
                .Property(e => e.To)
                .HasColumnType("text");

            modelBuilder.Entity<ArchivedEmail>()
                .Property(e => e.Cc)
                .HasColumnType("text");

            modelBuilder.Entity<ArchivedEmail>()
                .Property(e => e.Bcc)
                .HasColumnType("text");

            modelBuilder.Entity<ArchivedEmail>()
                .Property(e => e.Body)
                .HasColumnType("text");

            modelBuilder.Entity<ArchivedEmail>()
                .Property(e => e.HtmlBody)
                .HasColumnType("text");

            modelBuilder.Entity<ArchivedEmail>()
                .Property(e => e.MessageId)
                .HasColumnType("text");

            modelBuilder.Entity<ArchivedEmail>()
                .Property(e => e.FolderName)
                .HasColumnType("text");

            // Indizes NUR auf kleine oder eindeutige Felder setzen, NICHT auf Text-Felder
            // Entferne die Indizes von Subject, From, und To

            // Behalte nur Indizes auf kleinere Felder bei
            modelBuilder.Entity<ArchivedEmail>()
                .HasIndex(e => e.SentDate);

            modelBuilder.Entity<ArchivedEmail>()
                .HasIndex(e => e.MailAccountId);

            // Beziehungen
            modelBuilder.Entity<ArchivedEmail>()
                .HasOne(e => e.MailAccount)
                .WithMany(a => a.ArchivedEmails)
                .HasForeignKey(e => e.MailAccountId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<EmailAttachment>()
                .HasOne(a => a.ArchivedEmail)
                .WithMany(e => e.Attachments)
                .HasForeignKey(a => a.ArchivedEmailId)
                .OnDelete(DeleteBehavior.Cascade);

            // Konfiguration für Bytea (binäre Daten) für Anhänge
            modelBuilder.Entity<EmailAttachment>()
                .Property(a => a.Content)
                .HasColumnType("bytea");

            modelBuilder.Entity<EmailAttachment>()
                .Property(a => a.FileName)
                .HasColumnType("text");

            modelBuilder.Entity<EmailAttachment>()
                .Property(a => a.ContentType)
                .HasColumnType("text");
        }
    }
}
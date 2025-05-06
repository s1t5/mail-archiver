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

            // PostgreSQL Case-Insensitive Suche für bestimmte Spalten aktivieren
            modelBuilder.Entity<ArchivedEmail>()
                .Property(e => e.Subject)
                .HasColumnType("citext");

            modelBuilder.Entity<ArchivedEmail>()
                .Property(e => e.From)
                .HasColumnType("citext");

            modelBuilder.Entity<ArchivedEmail>()
                .Property(e => e.To)
                .HasColumnType("citext");

            // Indizes für schnelle Suche
            modelBuilder.Entity<ArchivedEmail>()
                .HasIndex(e => e.Subject);
            modelBuilder.Entity<ArchivedEmail>()
                .HasIndex(e => e.From);
            modelBuilder.Entity<ArchivedEmail>()
                .HasIndex(e => e.To);
            modelBuilder.Entity<ArchivedEmail>()
                .HasIndex(e => e.SentDate);

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
        }
    }
}
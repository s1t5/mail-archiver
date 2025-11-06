using MailArchiver.Models;
using Microsoft.EntityFrameworkCore;

namespace MailArchiver.Data
{
    public class MailArchiverDbContext : DbContext
    {
        public DbSet<MailAccount> MailAccounts { get; set; }
        public DbSet<ArchivedEmail> ArchivedEmails { get; set; }
        public DbSet<EmailAttachment> EmailAttachments { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<UserMailAccount> UserMailAccounts { get; set; }
        public DbSet<AccessLog> AccessLogs { get; set; }

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
                
            modelBuilder.Entity<EmailAttachment>()
                .Property(a => a.ContentId)
                .HasColumnType("text")
                .IsRequired(false);
            
            // User entity configuration
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();
                
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();
                
            modelBuilder.Entity<User>()
                .Property(u => u.Username)
                .HasMaxLength(50);
                
            modelBuilder.Entity<User>()
                .Property(u => u.Email)
                .HasMaxLength(100);
                
            // UserMailAccount entity configuration
            modelBuilder.Entity<UserMailAccount>()
                .HasIndex(uma => new { uma.UserId, uma.MailAccountId })
                .IsUnique();
                
            modelBuilder.Entity<UserMailAccount>()
                .HasOne(uma => uma.User)
                .WithMany(u => u.UserMailAccounts)
                .HasForeignKey(uma => uma.UserId)
                .OnDelete(DeleteBehavior.Cascade);
                
            modelBuilder.Entity<UserMailAccount>()
                .HasOne(uma => uma.MailAccount)
                .WithMany(ma => ma.UserMailAccounts)
                .HasForeignKey(uma => uma.MailAccountId)
                .OnDelete(DeleteBehavior.Cascade);
                
                
            // Configure Provider enum as string
            modelBuilder.Entity<MailAccount>()
                .Property(e => e.Provider)
                .HasConversion<string>()
                .HasMaxLength(10);
                
            // AccessLog entity configuration
            modelBuilder.Entity<AccessLog>()
                .Property(a => a.Username)
                .HasColumnType("text");
                
            modelBuilder.Entity<AccessLog>()
                .Property(a => a.EmailSubject)
                .HasColumnType("text")
                .IsRequired(false);
                
            modelBuilder.Entity<AccessLog>()
                .Property(a => a.EmailFrom)
                .HasColumnType("text")
                .IsRequired(false);
                
            modelBuilder.Entity<AccessLog>()
                .Property(a => a.SearchParameters)
                .HasColumnType("text")
                .IsRequired(false);
                
            modelBuilder.Entity<AccessLog>()
                .HasIndex(a => a.Timestamp)
                .HasDatabaseName("IX_AccessLogs_Timestamp");
                
            modelBuilder.Entity<AccessLog>()
                .HasIndex(a => a.Username)
                .HasDatabaseName("IX_AccessLogs_Username");
                
            modelBuilder.Entity<AccessLog>()
                .HasIndex(a => a.Type)
                .HasDatabaseName("IX_AccessLogs_Type");
                
            // Configure AccessLogType enum as integer
            modelBuilder.Entity<AccessLog>()
                .Property(a => a.Type)
                .HasConversion<int>();
                
            // Compliance fields configuration
            modelBuilder.Entity<ArchivedEmail>()
                .Property(e => e.ContentHash)
                .HasColumnType("varchar(64)");

            modelBuilder.Entity<ArchivedEmail>()
                .HasIndex(e => e.ContentHash)
                .HasDatabaseName("IX_ArchivedEmails_ContentHash");
        }
    }
}

using MailArchiver.Models;
using MailArchiver.Services.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MailArchiver.Data
{
    public class MailArchiverDbContext : DbContext
    {
        private readonly ICredentialProtector _credentialProtector;

        public DbSet<MailAccount> MailAccounts { get; set; }
        public DbSet<ArchivedEmail> ArchivedEmails { get; set; }
        public DbSet<EmailAttachment> EmailAttachments { get; set; }
        public DbSet<AttachmentContent> AttachmentContents { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<UserMailAccount> UserMailAccounts { get; set; }
        public DbSet<AccessLog> AccessLogs { get; set; }
        public DbSet<BandwidthUsage> BandwidthUsages { get; set; }
        public DbSet<SyncCheckpoint> SyncCheckpoints { get; set; }
        public DbSet<AccountStorageCache> AccountStorageCaches { get; set; }
        public DbSet<AccountStorageBackfillState> AccountStorageBackfillStates { get; set; }
        public DbSet<ApiKey> ApiKeys { get; set; }
        public DbSet<AuditExportJob> AuditExportJobs { get; set; }
        public DbSet<DashboardStatsCache> DashboardStatsCaches { get; set; }

        /// <summary>
        /// Used by design-time and unit tests that construct the context without a configured
        /// key. Credential values pass through unchanged (no encryption).
        /// </summary>
        public MailArchiverDbContext(DbContextOptions<MailArchiverDbContext> options)
            : this(options, NullCredentialProtector.Instance)
        {
        }

        public MailArchiverDbContext(DbContextOptions<MailArchiverDbContext> options, ICredentialProtector credentialProtector)
            : base(options)
        {
            _credentialProtector = credentialProtector;
        }

        /// <summary>
        /// The protector used by the credential value converters. Part of the model cache key
        /// so contexts with different protector states do not share a cached model.
        /// </summary>
        internal ICredentialProtector CredentialProtector => _credentialProtector;

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
                .Property(e => e.FromDisplayName)
                .HasColumnType("text")
                .IsRequired(false);

            modelBuilder.Entity<ArchivedEmail>()
                .Property(e => e.ToDisplayNames)
                .HasColumnType("text")
                .IsRequired(false);

            modelBuilder.Entity<ArchivedEmail>()
                .Property(e => e.CcDisplayNames)
                .HasColumnType("text")
                .IsRequired(false);

            modelBuilder.Entity<ArchivedEmail>()
                .Property(e => e.BccDisplayNames)
                .HasColumnType("text")
                .IsRequired(false);

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

            modelBuilder.Entity<ArchivedEmail>()
                .Property(e => e.RawHeaders)
                .HasColumnType("text")
                .IsRequired(false);

            modelBuilder.Entity<ArchivedEmail>()
                .Property(e => e.BodyUntruncatedText)
                .HasColumnType("text")
                .IsRequired(false);

            modelBuilder.Entity<ArchivedEmail>()
                .Property(e => e.BodyUntruncatedHtml)
                .HasColumnType("text")
                .IsRequired(false);

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

            // Konfiguration für Bytea (binäre Daten) für Anhänge (Legacy-Inline-Speicher).
            // Bildet die ursprüngliche "Content"-Spalte ab und bleibt während der
            // Dedup-Migration als nullable erhalten. Nach der Migration ist sie NULL.
            modelBuilder.Entity<EmailAttachment>()
                .Property(a => a.LegacyContent)
                .HasColumnName("Content")
                .HasColumnType("bytea")
                .IsRequired(false);

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

            // Attachment deduplication: reference from EmailAttachment to the shared
            // content-addressed AttachmentContent row. Restrict so that a content row
            // can never be deleted while still referenced; orphans are removed by the
            // dedup garbage collection in DatabaseMaintenanceService.
            modelBuilder.Entity<EmailAttachment>()
                .HasOne(a => a.AttachmentContent)
                .WithMany(c => c.Attachments)
                .HasForeignKey(a => a.AttachmentContentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<EmailAttachment>()
                .HasIndex(a => a.AttachmentContentId)
                .HasDatabaseName("IX_EmailAttachments_AttachmentContentId");

            // AttachmentContent entity configuration (content-addressed storage)
            modelBuilder.Entity<AttachmentContent>()
                .Property(c => c.Hash)
                .HasColumnType("varchar(64)")
                .IsRequired();

            modelBuilder.Entity<AttachmentContent>()
                .HasIndex(c => c.Hash)
                .IsUnique()
                .HasDatabaseName("IX_AttachmentContents_Hash");

            modelBuilder.Entity<AttachmentContent>()
                .Property(c => c.Content)
                .HasColumnType("bytea")
                .IsRequired();

            modelBuilder.Entity<AttachmentContent>()
                .Property(c => c.Size)
                .HasColumnType("bigint");


            modelBuilder.Entity<AttachmentContent>()
                .Property(c => c.CreatedAt)
                .HasColumnType("timestamp without time zone");

            modelBuilder.Entity<AttachmentContent>()
                .ToTable("AttachmentContents", "mail_archiver");


            // User entity configuration
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();
                
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();
                
            modelBuilder.Entity<User>()
                .Property(u => u.Username)
                .HasMaxLength(320);
                
            modelBuilder.Entity<User>()
                .Property(u => u.Email)
                .HasMaxLength(100);

            modelBuilder.Entity<User>()
                .Property(u => u.LastSeenChangelogVersion)
                .HasMaxLength(20);
                
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

            // Encrypt the sensitive credential columns at rest. The protector is captured in a
            // local so the compiled converter does not keep the DbContext instance alive in the
            // statically cached model. Legacy plain text values pass through on read and are
            // encrypted on the next write (or by CredentialEncryptionBackfillService).
            //
            // WARNING: because the encryption uses a random nonce, the same plain text maps to a
            // different ciphertext every time. As a consequence, EF cannot translate equality or
            // range predicates on these columns into matching SQL — e.g. Where(a => a.Password == "x")
            // silently returns no rows once encryption is active. Never filter, join or group by
            // these columns in LINQ; load the entities first, or match on a non-encrypted column.
            var credentialProtector = _credentialProtector;
            var credentialConverter = new ValueConverter<string?, string?>(
                value => credentialProtector.Protect(value),
                value => credentialProtector.Unprotect(value));
            var credentialComparer = new ValueComparer<string?>(
                (left, right) => left == right,
                value => value == null ? 0 : value.GetHashCode(),
                value => value);

            var mailAccountBuilder = modelBuilder.Entity<MailAccount>();
            foreach (var credentialProperty in new[]
            {
                nameof(MailAccount.Password),
                nameof(MailAccount.ClientSecret),
                nameof(MailAccount.OAuthRefreshToken),
                nameof(MailAccount.OAuthAccessToken)
            })
            {
                var property = mailAccountBuilder.Property<string?>(credentialProperty);
                property.HasConversion(credentialConverter);
                property.Metadata.SetValueComparer(credentialComparer);
            }

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

            // Configure IsLocked default value
            modelBuilder.Entity<ArchivedEmail>()
                .Property(e => e.IsLocked)
                .HasDefaultValue(false);

            // Original body content with null bytes preserved (stored as byte array)
            modelBuilder.Entity<ArchivedEmail>()
                .Property(e => e.OriginalBodyText)
                .HasColumnType("bytea")
                .IsRequired(false);

            modelBuilder.Entity<ArchivedEmail>()
                .Property(e => e.OriginalBodyHtml)
                .HasColumnType("bytea")
                .IsRequired(false);

            // BandwidthUsage entity configuration
            modelBuilder.Entity<BandwidthUsage>()
                .HasIndex(b => new { b.MailAccountId, b.Date })
                .IsUnique()
                .HasDatabaseName("IX_BandwidthUsage_Account_Date");

            modelBuilder.Entity<BandwidthUsage>()
                .HasIndex(b => b.Date)
                .HasDatabaseName("IX_BandwidthUsage_Date");

            modelBuilder.Entity<BandwidthUsage>()
                .Property(b => b.BytesDownloaded)
                .HasDefaultValue(0);

            modelBuilder.Entity<BandwidthUsage>()
                .Property(b => b.BytesUploaded)
                .HasDefaultValue(0);

            modelBuilder.Entity<BandwidthUsage>()
                .Property(b => b.EmailsProcessed)
                .HasDefaultValue(0);

            modelBuilder.Entity<BandwidthUsage>()
                .Property(b => b.LimitReached)
                .HasDefaultValue(false);

            modelBuilder.Entity<BandwidthUsage>()
                .HasOne(b => b.MailAccount)
                .WithMany()
                .HasForeignKey(b => b.MailAccountId)
                .OnDelete(DeleteBehavior.Cascade);

            // SyncCheckpoint entity configuration
            modelBuilder.Entity<SyncCheckpoint>()
                .HasIndex(s => new { s.MailAccountId, s.FolderName })
                .IsUnique()
                .HasDatabaseName("IX_SyncCheckpoints_Account_Folder");

            modelBuilder.Entity<SyncCheckpoint>()
                .HasIndex(s => s.MailAccountId)
                .HasDatabaseName("IX_SyncCheckpoints_AccountId");

            modelBuilder.Entity<SyncCheckpoint>()
                .Property(s => s.ProcessedCount)
                .HasDefaultValue(0);

            modelBuilder.Entity<SyncCheckpoint>()
                .Property(s => s.IsCompleted)
                .HasDefaultValue(false);

            modelBuilder.Entity<SyncCheckpoint>()
                .Property(s => s.BytesDownloaded)
                .HasDefaultValue(0);

            modelBuilder.Entity<SyncCheckpoint>()
                .HasOne(s => s.MailAccount)
                .WithMany()
                .HasForeignKey(s => s.MailAccountId)
                .OnDelete(DeleteBehavior.Cascade);

            // AccountStorageCache entity configuration
            modelBuilder.Entity<AccountStorageCache>()
                .HasKey(c => c.MailAccountId);

            modelBuilder.Entity<AccountStorageCache>()
                .Property(c => c.MailBytes)
                .HasColumnType("bigint")
                .HasDefaultValue(0);

            modelBuilder.Entity<AccountStorageCache>()
                .Property(c => c.AttachmentBytes)
                .HasColumnType("bigint")
                .HasDefaultValue(0);

            modelBuilder.Entity<AccountStorageCache>()
                .Property(c => c.TotalBytes)
                .HasColumnType("bigint")
                .HasDefaultValue(0);

            modelBuilder.Entity<AccountStorageCache>()
                .Property(c => c.UpdatedAt)
                .HasColumnType("timestamp with time zone");

            modelBuilder.Entity<AccountStorageCache>()
                .HasOne(c => c.MailAccount)
                .WithOne()
                .HasForeignKey<AccountStorageCache>(c => c.MailAccountId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AccountStorageCache>()
                .ToTable("AccountStorageCache", "mail_archiver");

            // DashboardStatsCache entity configuration (pre-computed dashboard
            // statistics, refreshed periodically by DashboardStatsRefreshService)
            modelBuilder.Entity<DashboardStatsCache>()
                .HasKey(c => c.Key);

            modelBuilder.Entity<DashboardStatsCache>()
                .Property(c => c.Key)
                .HasColumnType("text");

            modelBuilder.Entity<DashboardStatsCache>()
                .Property(c => c.TotalEmails)
                .HasColumnType("bigint");

            modelBuilder.Entity<DashboardStatsCache>()
                .Property(c => c.TotalAttachments)
                .HasColumnType("bigint");

            modelBuilder.Entity<DashboardStatsCache>()
                .Property(c => c.TotalAccounts)
                .HasColumnType("integer");

            modelBuilder.Entity<DashboardStatsCache>()
                .Property(c => c.TotalDatabaseSizeBytes)
                .HasColumnType("bigint");

            modelBuilder.Entity<DashboardStatsCache>()
                .Property(c => c.TopSendersJson)
                .HasColumnType("jsonb");

            modelBuilder.Entity<DashboardStatsCache>()
                .Property(c => c.EmailsByMonthJson)
                .HasColumnType("jsonb");

            modelBuilder.Entity<DashboardStatsCache>()
                .Property(c => c.EmailsPerAccountJson)
                .HasColumnType("jsonb");

            modelBuilder.Entity<DashboardStatsCache>()
                .Property(c => c.ComputedAtUtc)
                .HasColumnType("timestamp with time zone");

            modelBuilder.Entity<DashboardStatsCache>()
                .ToTable("DashboardStatsCache", "mail_archiver");

            // AccountStorageBackfillState entity configuration
            modelBuilder.Entity<AccountStorageBackfillState>()
                .HasKey(s => s.MailAccountId);

            modelBuilder.Entity<AccountStorageBackfillState>()
                .Property(s => s.Status)
                .HasColumnType("varchar(10)")
                .HasDefaultValue("Pending");

            modelBuilder.Entity<AccountStorageBackfillState>()
                .Property(s => s.CompletedAt)
                .HasColumnType("timestamp with time zone")
                .IsRequired(false);

            modelBuilder.Entity<AccountStorageBackfillState>()
                .HasOne(s => s.MailAccount)
                .WithOne()
                .HasForeignKey<AccountStorageBackfillState>(s => s.MailAccountId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AccountStorageBackfillState>()
                .ToTable("AccountStorageBackfillState", "mail_archiver");

            // ApiKey entity configuration (read-only REST API authentication)
            modelBuilder.Entity<ApiKey>()
                .Property(k => k.Name)
                .HasColumnType("text");

            modelBuilder.Entity<ApiKey>()
                .Property(k => k.KeyPrefix)
                .HasMaxLength(16);

            modelBuilder.Entity<ApiKey>()
                .Property(k => k.KeyHash)
                .HasColumnType("varchar(64)")
                .IsRequired();

            // SHA-256 hash is unique and the sole lookup key (O(1) auth).
            modelBuilder.Entity<ApiKey>()
                .HasIndex(k => k.KeyHash)
                .IsUnique()
                .HasDatabaseName("IX_ApiKeys_KeyHash");

            modelBuilder.Entity<ApiKey>()
                .HasIndex(k => k.UserId)
                .HasDatabaseName("IX_ApiKeys_UserId");

            modelBuilder.Entity<ApiKey>()
                .HasOne(k => k.User)
                .WithMany(u => u.ApiKeys)
                .HasForeignKey(k => k.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // AuditExportJob entity configuration (revision-safe export history)
            modelBuilder.Entity<AuditExportJob>()
                .Property(j => j.Id)
                .HasColumnType("uuid");

            modelBuilder.Entity<AuditExportJob>()
                .Property(j => j.Username)
                .HasColumnType("text");

            modelBuilder.Entity<AuditExportJob>()
                .Property(j => j.Status)
                .HasConversion<string>()
                .HasMaxLength(20);

            modelBuilder.Entity<AuditExportJob>()
                .Property(j => j.MailAccountName)
                .HasColumnType("text")
                .IsRequired(false);

            modelBuilder.Entity<AuditExportJob>()
                .Property(j => j.DataSupplierName)
                .HasColumnType("text")
                .IsRequired(false);

            modelBuilder.Entity<AuditExportJob>()
                .Property(j => j.DataSupplierLocation)
                .HasColumnType("text")
                .IsRequired(false);

            modelBuilder.Entity<AuditExportJob>()
                .Property(j => j.DataSupplierComment)
                .HasColumnType("text")
                .IsRequired(false);

            modelBuilder.Entity<AuditExportJob>()
                .Property(j => j.OutputFilePath)
                .HasColumnType("text")
                .IsRequired(false);

            modelBuilder.Entity<AuditExportJob>()
                .Property(j => j.ErrorMessage)
                .HasColumnType("text")
                .IsRequired(false);

            modelBuilder.Entity<AuditExportJob>()
                .HasIndex(j => j.Created)
                .HasDatabaseName("IX_AuditExportJobs_Created");

            modelBuilder.Entity<AuditExportJob>()
                .HasIndex(j => j.Status)
                .HasDatabaseName("IX_AuditExportJobs_Status");
        }
    }
}

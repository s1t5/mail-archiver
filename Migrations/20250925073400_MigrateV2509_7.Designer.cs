using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace MailArchiver.Migrations
{
    [DbContext(typeof(MailArchiver.Data.MailArchiverDbContext))]
    [Migration("20250925073400_MigrateV2509_7")]
    partial class MigrateV2509_7
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasDefaultSchema("mail_archiver")
                .HasAnnotation("ProductVersion", "8.0.7")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("MailArchiver.Models.AccessLog", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .UseIdentityByDefaultColumn();

                    b.Property<string>("EmailSubject")
                        .HasColumnType("text");

                    b.Property<int?>("EmailId")
                        .HasColumnType("integer");

                    b.Property<int?>("MailAccountId")
                        .HasColumnType("integer");

                    b.Property<string>("SearchParameters")
                        .HasColumnType("text");

                    b.Property<DateTime>("Timestamp")
                        .HasColumnType("timestamp without time zone");

                    b.Property<int>("Type")
                        .HasColumnType("integer");

                    b.Property<string>("Username")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.ToTable("AccessLogs", "mail_archiver");

                    b.HasIndex("Timestamp")
                        .HasDatabaseName("IX_AccessLogs_Timestamp");

                    b.HasIndex("Type")
                        .HasDatabaseName("IX_AccessLogs_Type");

                    b.HasIndex("Username")
                        .HasDatabaseName("IX_AccessLogs_Username");
                });

            modelBuilder.Entity("MailArchiver.Models.ArchivedEmail", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .UseIdentityByDefaultColumn();

                    b.Property<string>("Bcc")
                        .HasColumnType("text");

                    b.Property<string>("Body")
                        .HasColumnType("text");

                    b.Property<string>("Cc")
                        .HasColumnType("text");

                    b.Property<string>("FolderName")
                        .HasColumnType("text");

                    b.Property<string>("From")
                        .HasColumnType("text");

                    b.Property<bool>("HasAttachments")
                        .HasColumnType("boolean");

                    b.Property<bool>("IsOutgoing")
                        .HasColumnType("boolean");

                    b.Property<int>("MailAccountId")
                        .HasColumnType("integer");

                    b.Property<string>("MessageId")
                        .HasColumnType("text");

                    b.Property<DateTime>("ReceivedDate")
                        .HasColumnType("timestamp without time zone");

                    b.Property<DateTime>("SentDate")
                        .HasColumnType("timestamp without time zone");

                    b.Property<string>("Subject")
                        .HasColumnType("text");

                    b.Property<string>("To")
                        .HasColumnType("text");

                    b.Property<string>("HtmlBody")
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.HasIndex("MailAccountId");

                    b.HasIndex("SentDate");

                    b.ToTable("ArchivedEmails", "mail_archiver");

                    b.HasOne("MailArchiver.Models.MailAccount", "MailAccount")
                        .WithMany("ArchivedEmails")
                        .HasForeignKey("MailAccountId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("MailArchiver.Models.EmailAttachment", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .UseIdentityByDefaultColumn();

                    b.Property<int>("ArchivedEmailId")
                        .HasColumnType("integer");

                    b.Property<byte[]>("Content")
                        .IsRequired()
                        .HasColumnType("bytea");

                    b.Property<string>("ContentId")
                        .HasColumnType("text");

                    b.Property<string>("ContentType")
                        .HasColumnType("text");

                    b.Property<string>("FileName")
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.HasIndex("ArchivedEmailId");

                    b.ToTable("EmailAttachments", "mail_archiver");

                    b.HasOne("MailArchiver.Models.ArchivedEmail", "ArchivedEmail")
                        .WithMany("Attachments")
                        .HasForeignKey("ArchivedEmailId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("MailArchiver.Models.MailAccount", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .UseIdentityByDefaultColumn();

                    b.Property<string>("EmailAddress")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<bool>("IsEnabled")
                        .HasColumnType("boolean");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Provider")
                        .IsRequired()
                        .HasMaxLength(10)
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.ToTable("MailAccounts", "mail_archiver");
                });

            modelBuilder.Entity("MailArchiver.Models.User", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .UseIdentityByDefaultColumn();

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp without time zone");

                    b.Property<string>("Email")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<bool>("IsAdmin")
                        .HasColumnType("boolean");

                    b.Property<bool>("IsSelfManager")
                        .HasColumnType("boolean");

                    b.Property<bool>("IsActive")
                        .HasColumnType("boolean");

                    b.Property<DateTime?>("LastLoginAt")
                        .HasColumnType("timestamp without time zone");

                    b.Property<string>("PasswordHash")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<bool>("IsTwoFactorEnabled")
                        .HasColumnType("boolean");

                    b.Property<string>("TwoFactorBackupCodes")
                        .HasColumnType("text");

                    b.Property<string>("TwoFactorSecret")
                        .HasColumnType("text");

                    b.Property<string>("Username")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.HasKey("Id");

                    b.HasIndex("Email")
                        .IsUnique();

                    b.HasIndex("Username")
                        .IsUnique();

                    b.ToTable("Users", "mail_archiver");
                });

            modelBuilder.Entity("MailArchiver.Models.UserMailAccount", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .UseIdentityByDefaultColumn();

                    b.Property<int>("MailAccountId")
                        .HasColumnType("integer");

                    b.Property<int>("UserId")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.HasIndex("MailAccountId");

                    b.HasIndex("UserId", "MailAccountId")
                        .IsUnique();

                    b.ToTable("UserMailAccounts", "mail_archiver");

                    b.HasOne("MailArchiver.Models.MailAccount", "MailAccount")
                        .WithMany("UserMailAccounts")
                        .HasForeignKey("MailAccountId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("MailArchiver.Models.User", "User")
                        .WithMany("UserMailAccounts")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("MailArchiver.Models.ArchivedEmail", b =>
                {
                    b.HasOne("MailArchiver.Models.MailAccount", "MailAccount")
                        .WithMany("ArchivedEmails")
                        .HasForeignKey("MailAccountId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("MailAccount");
                });

            modelBuilder.Entity("MailArchiver.Models.EmailAttachment", b =>
                {
                    b.HasOne("MailArchiver.Models.ArchivedEmail", "ArchivedEmail")
                        .WithMany("Attachments")
                        .HasForeignKey("ArchivedEmailId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("ArchivedEmail");
                });

            modelBuilder.Entity("MailArchiver.Models.UserMailAccount", b =>
                {
                    b.HasOne("MailArchiver.Models.MailAccount", "MailAccount")
                        .WithMany("UserMailAccounts")
                        .HasForeignKey("MailAccountId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("MailArchiver.Models.User", "User")
                        .WithMany("UserMailAccounts")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("MailAccount");

                    b.Navigation("User");
                });

            modelBuilder.Entity("MailArchiver.Models.MailAccount", b =>
                {
                    b.Navigation("ArchivedEmails");

                    b.Navigation("UserMailAccounts");
                });

            modelBuilder.Entity("MailArchiver.Models.User", b =>
                {
                    b.Navigation("UserMailAccounts");
                });

            modelBuilder.Entity("MailArchiver.Models.ArchivedEmail", b =>
                {
                    b.Navigation("Attachments");
                });
#pragma warning restore 612, 618
        }
    }
}

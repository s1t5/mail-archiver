using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailArchiver.Migrations
{
    [DbContext(typeof(MailArchiver.Data.MailArchiverDbContext))]
    [Migration("20260224160000_MigrateV2602_4")]
    partial class MigrateV2602_4
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasDefaultSchema("mail_archiver")
                .HasAnnotation("ProductVersion", "9.0.4")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);
            
            // Simplified BuildTargetModel - full model snapshot is in MailArchiverDbContextModelSnapshot.cs
#pragma warning restore 612, 618
        }
    }
}
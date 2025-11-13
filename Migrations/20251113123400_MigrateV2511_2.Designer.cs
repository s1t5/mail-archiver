using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailArchiver.Migrations
{
    [DbContext(typeof(MailArchiver.Data.MailArchiverDbContext))]
    [Migration("20251113123400_MigrateV2511_2")]
    partial class MigrateV2511_2
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

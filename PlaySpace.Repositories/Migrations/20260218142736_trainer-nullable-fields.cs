using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PlaySpace.Repositories.Data;

#nullable disable

namespace PlaySpace.Repositories.Migrations
{
    [DbContext(typeof(PlaySpaceDbContext))]
    [Migration("20260218142736_trainer-nullable-fields")]
    public partial class trainernullablefields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"TrainerProfiles\" ALTER COLUMN \"Nip\" DROP NOT NULL");
            migrationBuilder.Sql("ALTER TABLE \"TrainerProfiles\" ALTER COLUMN \"CompanyName\" DROP NOT NULL");
            migrationBuilder.Sql("ALTER TABLE \"TrainerProfiles\" ALTER COLUMN \"Address\" DROP NOT NULL");
            migrationBuilder.Sql("ALTER TABLE \"TrainerProfiles\" ALTER COLUMN \"City\" DROP NOT NULL");
            migrationBuilder.Sql("ALTER TABLE \"TrainerProfiles\" ALTER COLUMN \"PostalCode\" DROP NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE \"TrainerProfiles\" SET \"Nip\" = '' WHERE \"Nip\" IS NULL");
            migrationBuilder.Sql("UPDATE \"TrainerProfiles\" SET \"CompanyName\" = '' WHERE \"CompanyName\" IS NULL");
            migrationBuilder.Sql("UPDATE \"TrainerProfiles\" SET \"Address\" = '' WHERE \"Address\" IS NULL");
            migrationBuilder.Sql("UPDATE \"TrainerProfiles\" SET \"City\" = '' WHERE \"City\" IS NULL");
            migrationBuilder.Sql("UPDATE \"TrainerProfiles\" SET \"PostalCode\" = '' WHERE \"PostalCode\" IS NULL");
            migrationBuilder.Sql("ALTER TABLE \"TrainerProfiles\" ALTER COLUMN \"Nip\" SET NOT NULL");
            migrationBuilder.Sql("ALTER TABLE \"TrainerProfiles\" ALTER COLUMN \"CompanyName\" SET NOT NULL");
            migrationBuilder.Sql("ALTER TABLE \"TrainerProfiles\" ALTER COLUMN \"Address\" SET NOT NULL");
            migrationBuilder.Sql("ALTER TABLE \"TrainerProfiles\" ALTER COLUMN \"City\" SET NOT NULL");
            migrationBuilder.Sql("ALTER TABLE \"TrainerProfiles\" ALTER COLUMN \"PostalCode\" SET NOT NULL");
        }
    }
}

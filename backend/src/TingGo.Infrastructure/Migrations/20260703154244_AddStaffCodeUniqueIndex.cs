using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TingGo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffCodeUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_memberships_venue_id_staff_code",
                table: "memberships",
                columns: new[] { "venue_id", "staff_code" },
                unique: true,
                filter: "staff_code IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_memberships_venue_id_staff_code",
                table: "memberships");
        }
    }
}

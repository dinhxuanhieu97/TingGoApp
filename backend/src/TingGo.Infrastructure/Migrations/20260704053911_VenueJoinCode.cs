using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TingGo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class VenueJoinCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "join_code",
                table: "venues",
                type: "character varying(6)",
                maxLength: 6,
                nullable: false,
                defaultValue: "");

            // Backfill mã quán duy nhất cho venue có sẵn trước khi tạo unique index
            migrationBuilder.Sql("""
                UPDATE venues SET join_code = upper(
                    translate(substr(md5(id::text || random()::text), 1, 6),
                              '01ilo', '23456'))
                WHERE join_code = '';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_venues_join_code",
                table: "venues",
                column: "join_code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_venues_join_code",
                table: "venues");

            migrationBuilder.DropColumn(
                name: "join_code",
                table: "venues");
        }
    }
}

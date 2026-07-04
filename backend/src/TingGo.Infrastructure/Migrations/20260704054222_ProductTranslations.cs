using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TingGo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ProductTranslations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "product_translations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    locale = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_translations", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_product_translations_product_id_locale",
                table: "product_translations",
                columns: new[] { "product_id", "locale" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_translations");
        }
    }
}

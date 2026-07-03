using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TingGo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "menu_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    menu_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_visible = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_menu_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "menus",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    venue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_menus", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "modifier_groups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    venue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    min_select = table.Column<int>(type: "integer", nullable: false),
                    max_select = table.Column<int>(type: "integer", nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_modifier_groups", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "modifier_options",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    modifier_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    price_delta_minor = table.Column<long>(type: "bigint", nullable: false),
                    is_available = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_modifier_options", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "product_modifier_groups",
                columns: table => new
                {
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    modifier_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_modifier_groups", x => new { x.product_id, x.modifier_group_id });
                });

            migrationBuilder.CreateTable(
                name: "product_variants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    price_delta_minor = table.Column<long>(type: "bigint", nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    is_available = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_variants", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    venue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    base_price_minor = table.Column<long>(type: "bigint", nullable: false),
                    currency_code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    image_url = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    is_available = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_menu_categories_menu_id",
                table: "menu_categories",
                column: "menu_id");

            migrationBuilder.CreateIndex(
                name: "IX_menus_venue_id",
                table: "menus",
                column: "venue_id");

            migrationBuilder.CreateIndex(
                name: "IX_modifier_groups_venue_id",
                table: "modifier_groups",
                column: "venue_id");

            migrationBuilder.CreateIndex(
                name: "IX_modifier_options_modifier_group_id",
                table: "modifier_options",
                column: "modifier_group_id");

            migrationBuilder.CreateIndex(
                name: "IX_product_variants_product_id",
                table: "product_variants",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_venue_id_category_id_status",
                table: "products",
                columns: new[] { "venue_id", "category_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_products_venue_id_is_available",
                table: "products",
                columns: new[] { "venue_id", "is_available" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "menu_categories");

            migrationBuilder.DropTable(
                name: "menus");

            migrationBuilder.DropTable(
                name: "modifier_groups");

            migrationBuilder.DropTable(
                name: "modifier_options");

            migrationBuilder.DropTable(
                name: "product_modifier_groups");

            migrationBuilder.DropTable(
                name: "product_variants");

            migrationBuilder.DropTable(
                name: "products");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TingGo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class QuickImportStaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "import_issues",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    import_job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    import_row_id = table.Column<Guid>(type: "uuid", nullable: true),
                    severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    sheet_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    row_number = table.Column<int>(type: "integer", nullable: true),
                    field_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    message = table.Column<string>(type: "text", nullable: false),
                    suggested_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_issues", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "import_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    venue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    import_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    template_version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    original_filename = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    total_rows = table.Column<int>(type: "integer", nullable: false),
                    valid_rows = table.Column<int>(type: "integer", nullable: false),
                    warning_rows = table.Column<int>(type: "integer", nullable: false),
                    error_rows = table.Column<int>(type: "integer", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_jobs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "import_rows",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    import_job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    section_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sheet_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    row_number = table.Column<int>(type: "integer", nullable: false),
                    source_data = table.Column<string>(type: "jsonb", nullable: false),
                    normalized_data = table.Column<string>(type: "jsonb", nullable: false),
                    row_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    entity_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_rows", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_import_issues_import_job_id",
                table: "import_issues",
                column: "import_job_id");

            migrationBuilder.CreateIndex(
                name: "IX_import_jobs_venue_id_created_at",
                table: "import_jobs",
                columns: new[] { "venue_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_import_rows_import_job_id_section_type",
                table: "import_rows",
                columns: new[] { "import_job_id", "section_type" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "import_issues");

            migrationBuilder.DropTable(
                name: "import_jobs");

            migrationBuilder.DropTable(
                name: "import_rows");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PasteList.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sync_configurations",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    sync_type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    is_enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    config_data = table.Column<string>(type: "TEXT", maxLength: 5000, nullable: true),
                    last_sync_time = table.Column<DateTime>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sync_configurations", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sync_configurations");
        }
    }
}

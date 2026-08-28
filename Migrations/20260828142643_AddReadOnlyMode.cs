using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PangolinWatchdog.Migrations
{
    /// <inheritdoc />
    public partial class AddReadOnlyMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ReadOnlyMode",
                table: "Configurations",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReadOnlyMode",
                table: "Configurations");
        }
    }
}

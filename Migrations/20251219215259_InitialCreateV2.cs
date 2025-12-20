using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PangolinWatchdog.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreateV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TargetResourceId",
                table: "Rules",
                newName: "TargetResourceName");

            migrationBuilder.RenameColumn(
                name: "ExcludedResourceIds",
                table: "Rules",
                newName: "ExcludedResourceNames");

            migrationBuilder.AlterColumn<long>(
                name: "ResourceId",
                table: "BannedIps",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TargetResourceName",
                table: "Rules",
                newName: "TargetResourceId");

            migrationBuilder.RenameColumn(
                name: "ExcludedResourceNames",
                table: "Rules",
                newName: "ExcludedResourceIds");

            migrationBuilder.AlterColumn<string>(
                name: "ResourceId",
                table: "BannedIps",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "INTEGER");
        }
    }
}

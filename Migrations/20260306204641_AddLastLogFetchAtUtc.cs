using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PangolinWatchdog.Migrations
{
    /// <inheritdoc />
    public partial class AddLastLogFetchAtUtc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastLogFetchAtUtc",
                table: "Configurations",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastLogFetchAtUtc",
                table: "Configurations");
        }
    }
}

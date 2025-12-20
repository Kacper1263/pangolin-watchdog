using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PangolinWatchdog.Migrations
{
    /// <inheritdoc />
    public partial class AddManyToManyExclusions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RuleResourceExclusions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RuleId = table.Column<long>(type: "INTEGER", nullable: false),
                    ResourceId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuleResourceExclusions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RuleResourceExclusions_Resources_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RuleResourceExclusions_Rules_RuleId",
                        column: x => x.RuleId,
                        principalTable: "Rules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Migrate existing ExcludedResourceIds (comma-separated) to junction table
            // ExcludedResourceIds could contain resource IDs separated by commas
            migrationBuilder.Sql(@"
                INSERT INTO RuleResourceExclusions (RuleId, ResourceId)
                SELECT 
                    r.Id as RuleId,
                    res.Id as ResourceId
                FROM Rules r
                CROSS JOIN Resources res
                WHERE r.ExcludedResourceIds IS NOT NULL 
                  AND r.ExcludedResourceIds != ''
                  AND r.IsGlobal = 1
                  AND (',' || r.ExcludedResourceIds || ',') LIKE ('%,' || CAST(res.Id AS TEXT) || ',%')
            ");

            migrationBuilder.DropColumn(
                name: "ExcludedResourceIds",
                table: "Rules");

            migrationBuilder.CreateIndex(
                name: "IX_RuleResourceExclusions_ResourceId",
                table: "RuleResourceExclusions",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_RuleResourceExclusions_RuleId",
                table: "RuleResourceExclusions",
                column: "RuleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RuleResourceExclusions");

            migrationBuilder.AddColumn<string>(
                name: "ExcludedResourceIds",
                table: "Rules",
                type: "TEXT",
                nullable: true);
        }
    }
}

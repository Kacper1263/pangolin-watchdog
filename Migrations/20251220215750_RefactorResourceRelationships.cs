using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PangolinWatchdog.Migrations
{
    /// <inheritdoc />
    public partial class RefactorResourceRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create Resources table first
            migrationBuilder.CreateTable(
                name: "Resources",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PangolinResourceId = table.Column<long>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    FullDomain = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Resources", x => x.Id);
                });

            // Migrate existing data from BannedIps.ResourceId to Resources table
            migrationBuilder.Sql(@"
                INSERT INTO Resources (PangolinResourceId, Name, FullDomain)
                SELECT DISTINCT ResourceId, COALESCE(ResourceName, 'Unknown Resource'), 'unknown.domain'
                FROM BannedIps
                WHERE ResourceId NOT IN (SELECT PangolinResourceId FROM Resources);
            ");

            // Migrate existing data from Rules.TargetResourceName (resource names, not IDs) to Resources
            migrationBuilder.Sql(@"
                INSERT INTO Resources (PangolinResourceId, Name, FullDomain)
                SELECT DISTINCT 
                    0 - ROW_NUMBER() OVER (ORDER BY TargetResourceName), 
                    TargetResourceName, 
                    'unknown.domain'
                FROM Rules
                WHERE TargetResourceName IS NOT NULL 
                  AND TargetResourceName != ''
                  AND NOT IsGlobal
                  AND TargetResourceName NOT IN (SELECT Name FROM Resources);
            ");

            // Create temporary table for BannedIps with new FK
            migrationBuilder.Sql(@"
                CREATE TABLE BannedIps_Temp (
                    Id INTEGER PRIMARY KEY,
                    IpAddress TEXT NOT NULL,
                    Reason TEXT NOT NULL,
                    ResourceId INTEGER NOT NULL,
                    BannedAt TEXT NOT NULL,
                    ExpiresAt TEXT,
                    FOREIGN KEY (ResourceId) REFERENCES Resources(Id) ON DELETE CASCADE
                );
            ");

            // Copy data to temp table with FK mapping
            migrationBuilder.Sql(@"
                INSERT INTO BannedIps_Temp (Id, IpAddress, Reason, ResourceId, BannedAt, ExpiresAt)
                SELECT b.Id, b.IpAddress, b.Reason, r.Id, b.BannedAt, b.ExpiresAt
                FROM BannedIps b
                INNER JOIN Resources r ON r.PangolinResourceId = b.ResourceId;
            ");

            // Drop old table and rename temp
            migrationBuilder.DropTable(name: "BannedIps");
            
            migrationBuilder.Sql("ALTER TABLE BannedIps_Temp RENAME TO BannedIps;");

            // Drop old columns from Rules
            migrationBuilder.DropColumn(
                name: "ExcludedResourceNames",
                table: "Rules");

            // Rename and add new column
            migrationBuilder.RenameColumn(
                name: "TargetResourceName",
                table: "Rules",
                newName: "ExcludedResourceIds");

            migrationBuilder.AddColumn<long>(
                name: "TargetResourceId",
                table: "Rules",
                type: "INTEGER",
                nullable: true);

            // Update TargetResourceId based on old TargetResourceName (it was resource NAME, not ID)
            migrationBuilder.Sql(@"
                UPDATE Rules
                SET TargetResourceId = (
                    SELECT Id FROM Resources 
                    WHERE Name = ExcludedResourceIds
                )
                WHERE ExcludedResourceIds IS NOT NULL 
                  AND ExcludedResourceIds != ''
                  AND NOT IsGlobal;
            ");

            // Clear ExcludedResourceIds for non-global rules (it was TargetResourceName before rename)
            migrationBuilder.Sql(@"
                UPDATE Rules
                SET ExcludedResourceIds = NULL
                WHERE NOT IsGlobal;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_Rules_TargetResourceId",
                table: "Rules",
                column: "TargetResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_BannedIps_ResourceId",
                table: "BannedIps",
                column: "ResourceId");

            migrationBuilder.AddForeignKey(
                name: "FK_Rules_Resources_TargetResourceId",
                table: "Rules",
                column: "TargetResourceId",
                principalTable: "Resources",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BannedIps_Resources_ResourceId",
                table: "BannedIps");

            migrationBuilder.DropForeignKey(
                name: "FK_Rules_Resources_TargetResourceId",
                table: "Rules");

            migrationBuilder.DropTable(
                name: "Resources");

            migrationBuilder.DropIndex(
                name: "IX_Rules_TargetResourceId",
                table: "Rules");

            migrationBuilder.DropIndex(
                name: "IX_BannedIps_ResourceId",
                table: "BannedIps");

            migrationBuilder.DropColumn(
                name: "TargetResourceId",
                table: "Rules");

            migrationBuilder.RenameColumn(
                name: "ExcludedResourceIds",
                table: "Rules",
                newName: "TargetResourceName");

            migrationBuilder.AddColumn<string>(
                name: "ExcludedResourceNames",
                table: "Rules",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResourceName",
                table: "BannedIps",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }
    }
}

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StackMeet.Api.Data;

#nullable disable

namespace StackMeet.Api.Migrations
{
    [DbContext(typeof(StackMeetDbContext))]
    [Migration("20260914093000_FinalsRankingGovernanceSp4g")]
    public partial class FinalsRankingGovernanceSp4g : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // This immutable evidence ledger is intentionally migration-managed rather than
            // EF-tracked. SP-4G accesses it through a narrow service boundary so ordinary
            // competition queries never hydrate the potentially large snapshot JSON.
            migrationBuilder.CreateTable(
                name: "FinalsRankingGovernance",
                schema: "dbo",
                columns: table => new
                {
                    CompetitionId = table.Column<int>(type: "int", nullable: false),
                    RuleVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RuleSelectedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RuleSelectedByUserId = table.Column<int>(type: "int", nullable: true),
                    SnapshotSchemaVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SourceStateRevision = table.Column<long>(type: "bigint", nullable: true),
                    SourceResultsRevision = table.Column<long>(type: "bigint", nullable: true),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SnapshotSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SnapshotCapturedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SnapshotCapturedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinalsRankingGovernance", x => x.CompetitionId);
                    table.ForeignKey(
                        name: "FK_FinalsRankingGovernance_Competition_CompetitionId",
                        column: x => x.CompetitionId,
                        principalSchema: "dbo",
                        principalTable: "Competition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(@"
ALTER TABLE [dbo].[FinalsRankingGovernance]
ADD CONSTRAINT [CK_FinalsRankingGovernance_RuleVersion]
CHECK ([RuleVersion] IN ('legacy-finals-v1', 'governed-finals-v2')); ");

            migrationBuilder.Sql(@"
ALTER TABLE [dbo].[FinalsRankingGovernance]
ADD CONSTRAINT [CK_FinalsRankingGovernance_SnapshotCompleteness]
CHECK (
    ([SnapshotCapturedAt] IS NULL
        AND [SnapshotSchemaVersion] IS NULL
        AND [SourceStateRevision] IS NULL
        AND [SourceResultsRevision] IS NULL
        AND [SnapshotJson] IS NULL
        AND [SnapshotSha256] IS NULL)
    OR
    ([SnapshotCapturedAt] IS NOT NULL
        AND [SnapshotSchemaVersion] IS NOT NULL
        AND [SourceStateRevision] IS NOT NULL
        AND [SourceResultsRevision] IS NOT NULL
        AND [SnapshotJson] IS NOT NULL
        AND [SnapshotSha256] IS NOT NULL)
); ");

            migrationBuilder.Sql(@"
CREATE TRIGGER [dbo].[TR_FinalsRankingGovernance_ImmutableSnapshot]
ON [dbo].[FinalsRankingGovernance]
AFTER UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM deleted WHERE [SnapshotCapturedAt] IS NOT NULL)
    BEGIN
        THROW 51041, 'Captured Finals ranking governance records are immutable.', 1;
    END
END; ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS [dbo].[TR_FinalsRankingGovernance_ImmutableSnapshot];");
            migrationBuilder.DropTable(
                name: "FinalsRankingGovernance",
                schema: "dbo");
        }
    }
}

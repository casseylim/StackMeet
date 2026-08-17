using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StackMeet.Api.Migrations
{
    /// <inheritdoc />
    public partial class CompetitionResultsPhase1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ResultsRevision",
                schema: "dbo",
                table: "Competition",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "CompetitionResult",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompetitionId = table.Column<int>(type: "int", nullable: false),
                    Stage = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ParticipantType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ParticipantCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EventCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AttemptsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Penalty = table.Column<decimal>(type: "decimal(12,3)", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionResult", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetitionResult_Competition_CompetitionId",
                        column: x => x.CompetitionId,
                        principalSchema: "dbo",
                        principalTable: "Competition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionResult_PublicId",
                schema: "dbo",
                table: "CompetitionResult",
                column: "PublicId",
                unique: true);

            // Backfill the legacy result rows before the new API is enabled. Invalid or
            // incomplete legacy rows are intentionally skipped and remain in JsonData
            // for manual review rather than breaking the migration.
            migrationBuilder.Sql(@"
;WITH LegacyResults AS
(
    SELECT c.Id AS CompetitionId, r.Stage, r.[Type] AS ParticipantType,
           r.Participant, r.Event, r.Attempts, r.Penalty,
           ROW_NUMBER() OVER (PARTITION BY c.Id, r.Stage, r.[Type], r.Participant, r.Event ORDER BY (SELECT 1)) AS DuplicateRank
    FROM dbo.Competition c
    INNER JOIN dbo.CompetitionState s ON s.CompetitionKey = c.CompetitionKey
    CROSS APPLY OPENJSON(s.JsonData, '$.results')
    WITH
    (
        Stage nvarchar(30) '$.stage', [Type] nvarchar(30) '$.type',
        Participant nvarchar(50) '$.participant', Event nvarchar(50) '$.event',
        Attempts nvarchar(max) '$.attempts' AS JSON, Penalty decimal(12,3) '$.penalty'
    ) r
    WHERE r.Stage IS NOT NULL AND r.[Type] IS NOT NULL AND r.Participant IS NOT NULL AND r.Event IS NOT NULL
)
INSERT dbo.CompetitionResult (PublicId, CompetitionId, Stage, ParticipantType, ParticipantCode, EventCode, AttemptsJson, Penalty, Revision, CreatedAt, UpdatedAt)
SELECT NEWID(), CompetitionId, Stage, ParticipantType, Participant, Event, COALESCE(Attempts, '[]'), COALESCE(Penalty, 0), 1, SYSUTCDATETIME(), SYSUTCDATETIME()
FROM LegacyResults WHERE DuplicateRank = 1;
UPDATE dbo.Competition SET ResultsRevision = 1
WHERE Id IN (SELECT DISTINCT CompetitionId FROM dbo.CompetitionResult);");

            migrationBuilder.CreateIndex(
                name: "UX_CompetitionResult_LogicalResult",
                schema: "dbo",
                table: "CompetitionResult",
                columns: new[] { "CompetitionId", "Stage", "ParticipantType", "ParticipantCode", "EventCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompetitionResult",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "ResultsRevision",
                schema: "dbo",
                table: "Competition");
        }
    }
}

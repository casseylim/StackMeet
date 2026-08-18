using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StackMeet.Api.Migrations
{
    /// <inheritdoc />
    public partial class CertificateTemplatesPhase1A : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CertificateTemplate",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompetitionId = table.Column<int>(type: "int", nullable: false),
                    CertificateType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    StoredFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TemplateVersion = table.Column<int>(type: "int", nullable: false),
                    TemplateSchemaVersion = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertificateTemplate", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CertificateTemplate_AppUser_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "dbo",
                        principalTable: "AppUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CertificateTemplate_AppUser_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalSchema: "dbo",
                        principalTable: "AppUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CertificateTemplate_Competition_CompetitionId",
                        column: x => x.CompetitionId,
                        principalSchema: "dbo",
                        principalTable: "Competition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CertificateTemplate_CompetitionId_CertificateType",
                schema: "dbo",
                table: "CertificateTemplate",
                columns: new[] { "CompetitionId", "CertificateType" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_CertificateTemplate_CompetitionId_CertificateType_TemplateVersion",
                schema: "dbo",
                table: "CertificateTemplate",
                columns: new[] { "CompetitionId", "CertificateType", "TemplateVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CertificateTemplate_CreatedByUserId",
                schema: "dbo",
                table: "CertificateTemplate",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CertificateTemplate_UpdatedByUserId",
                schema: "dbo",
                table: "CertificateTemplate",
                column: "UpdatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CertificateTemplate",
                schema: "dbo");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace StackMeet.Api.Migrations
{
    /// <inheritdoc />
    public partial class AccountAccessPhase1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppRole",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppRole", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppUser",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsSystemAdmin = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUser", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLog",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    CompetitionId = table.Column<int>(type: "int", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OldValueJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValueJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLog", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditLog_AppUser_UserId",
                        column: x => x.UserId,
                        principalSchema: "dbo",
                        principalTable: "AppUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AuditLog_Competition_CompetitionId",
                        column: x => x.CompetitionId,
                        principalSchema: "dbo",
                        principalTable: "Competition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionUser",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompetitionId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AssignedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionUser", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetitionUser_AppRole_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "dbo",
                        principalTable: "AppRole",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompetitionUser_AppUser_AssignedByUserId",
                        column: x => x.AssignedByUserId,
                        principalSchema: "dbo",
                        principalTable: "AppUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompetitionUser_AppUser_UserId",
                        column: x => x.UserId,
                        principalSchema: "dbo",
                        principalTable: "AppUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompetitionUser_Competition_CompetitionId",
                        column: x => x.CompetitionId,
                        principalSchema: "dbo",
                        principalTable: "Competition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "AppRole",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "SystemAdmin" },
                    { 2, "CompetitionManager" },
                    { 3, "DataEntry" },
                    { 4, "Viewer" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppRole_Name",
                schema: "dbo",
                table: "AppRole",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppUser_NormalizedEmail",
                schema: "dbo",
                table: "AppUser",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_CompetitionId_CreatedAt",
                schema: "dbo",
                table: "AuditLog",
                columns: new[] { "CompetitionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_UserId",
                schema: "dbo",
                table: "AuditLog",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionUser_AssignedByUserId",
                schema: "dbo",
                table: "CompetitionUser",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionUser_CompetitionId_UserId",
                schema: "dbo",
                table: "CompetitionUser",
                columns: new[] { "CompetitionId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionUser_RoleId",
                schema: "dbo",
                table: "CompetitionUser",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionUser_UserId",
                schema: "dbo",
                table: "CompetitionUser",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLog",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CompetitionUser",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AppRole",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AppUser",
                schema: "dbo");
        }
    }
}

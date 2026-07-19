using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StackMeet.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCompetitionAndStacker : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Competition",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompetitionCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CompetitionName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Venue = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Competition", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Stacker",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompetitionId = table.Column<int>(type: "int", nullable: false),
                    StackerCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    WssaId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Gender = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BirthDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Country = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Club = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsSpecialStacker = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stacker", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Stacker_Competition_CompetitionId",
                        column: x => x.CompetitionId,
                        principalSchema: "dbo",
                        principalTable: "Competition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Competition_CompetitionCode",
                schema: "dbo",
                table: "Competition",
                column: "CompetitionCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stacker_CompetitionId_StackerCode",
                schema: "dbo",
                table: "Stacker",
                columns: new[] { "CompetitionId", "StackerCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Stacker",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Competition",
                schema: "dbo");
        }
    }
}

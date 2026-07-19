using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StackMeet.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.CreateTable(
                name: "CompetitionState",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompetitionKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    JsonData = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SchemaVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "0.9-online"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionState", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionState_CompetitionKey",
                schema: "dbo",
                table: "CompetitionState",
                column: "CompetitionKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompetitionState",
                schema: "dbo");
        }
    }
}

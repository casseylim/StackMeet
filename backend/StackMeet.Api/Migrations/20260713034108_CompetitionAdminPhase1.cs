using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StackMeet.Api.Migrations
{
    /// <inheritdoc />
    public partial class CompetitionAdminPhase1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "dbo",
                table: "CompetitionState",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.Sql("UPDATE [dbo].[CompetitionState] SET [CreatedAt] = [UpdatedAt] WHERE [CreatedAt] IS NULL OR [CreatedAt] = '0001-01-01T00:00:00.0000000'");

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                schema: "dbo",
                table: "Competition",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedBy",
                schema: "dbo",
                table: "Competition",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompetitionKey",
                schema: "dbo",
                table: "Competition",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql("UPDATE [dbo].[Competition] SET [CompetitionKey] = UPPER([CompetitionCode]) WHERE [CompetitionKey] IS NULL OR LTRIM(RTRIM([CompetitionKey])) = ''");

            migrationBuilder.AlterColumn<string>(
                name: "CompetitionKey",
                schema: "dbo",
                table: "Competition",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                schema: "dbo",
                table: "Competition",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Competition_CompetitionKey",
                schema: "dbo",
                table: "Competition",
                column: "CompetitionKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Competition_CompetitionKey",
                schema: "dbo",
                table: "Competition");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "dbo",
                table: "CompetitionState");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                schema: "dbo",
                table: "Competition");

            migrationBuilder.DropColumn(
                name: "ArchivedBy",
                schema: "dbo",
                table: "Competition");

            migrationBuilder.DropColumn(
                name: "CompetitionKey",
                schema: "dbo",
                table: "Competition");

            migrationBuilder.DropColumn(
                name: "PasswordHash",
                schema: "dbo",
                table: "Competition");
        }
    }
}
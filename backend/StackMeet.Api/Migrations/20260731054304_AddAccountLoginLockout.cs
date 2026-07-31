using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StackMeet.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountLoginLockout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FailedLoginAttempts",
                schema: "dbo",
                table: "AppUser",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsPermanentlyLocked",
                schema: "dbo",
                table: "AppUser",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockoutUntil",
                schema: "dbo",
                table: "AppUser",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LoginLockoutRound",
                schema: "dbo",
                table: "AppUser",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FailedLoginAttempts",
                schema: "dbo",
                table: "AppUser");

            migrationBuilder.DropColumn(
                name: "IsPermanentlyLocked",
                schema: "dbo",
                table: "AppUser");

            migrationBuilder.DropColumn(
                name: "LockoutUntil",
                schema: "dbo",
                table: "AppUser");

            migrationBuilder.DropColumn(
                name: "LoginLockoutRound",
                schema: "dbo",
                table: "AppUser");
        }
    }
}

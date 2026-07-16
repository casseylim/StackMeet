using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StackMeet.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddStackerRegistrationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CheckedIn",
                schema: "dbo",
                table: "Stacker",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "No");

            migrationBuilder.AddColumn<string>(
                name: "CustomDivision",
                schema: "dbo",
                table: "Stacker",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                schema: "dbo",
                table: "Stacker",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Paid",
                schema: "dbo",
                table: "Stacker",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "No");

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                schema: "dbo",
                table: "Stacker",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Region",
                schema: "dbo",
                table: "Stacker",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CheckedIn",
                schema: "dbo",
                table: "Stacker");

            migrationBuilder.DropColumn(
                name: "CustomDivision",
                schema: "dbo",
                table: "Stacker");

            migrationBuilder.DropColumn(
                name: "Email",
                schema: "dbo",
                table: "Stacker");

            migrationBuilder.DropColumn(
                name: "Paid",
                schema: "dbo",
                table: "Stacker");

            migrationBuilder.DropColumn(
                name: "Phone",
                schema: "dbo",
                table: "Stacker");

            migrationBuilder.DropColumn(
                name: "Region",
                schema: "dbo",
                table: "Stacker");
        }
    }
}

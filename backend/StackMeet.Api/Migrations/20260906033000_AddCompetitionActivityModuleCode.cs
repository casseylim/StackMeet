using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StackMeet.Api.Data;

#nullable disable

namespace StackMeet.Api.Migrations
{
    [DbContext(typeof(StackMeetDbContext))]
    [Migration("20260906033000_AddCompetitionActivityModuleCode")]
    public partial class AddCompetitionActivityModuleCode : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActivityModuleCode",
                schema: "dbo",
                table: "Competition",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActivityModuleCode",
                schema: "dbo",
                table: "Competition");
        }
    }
}

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StackMeet.Api.Data;

#nullable disable

namespace StackMeet.Api.Migrations
{
    [DbContext(typeof(StackMeetDbContext))]
    [Migration("20260910103000_StackerIdentityPersistenceV1")]
    public partial class StackerIdentityPersistenceV1 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SportStackerIdentity",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NadiTrackId = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: false),
                    WssaId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Gender = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BirthDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Country = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Club = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Region = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsPublicProfile = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SportStackerIdentity", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StackerIdentityLink",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SportStackerIdentityId = table.Column<long>(type: "bigint", nullable: false),
                    StackerId = table.Column<int>(type: "int", nullable: false),
                    MatchMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ResolutionReasonCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ResolutionNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LinkedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LinkedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StackerIdentityLink", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StackerIdentityLink_SportStackerIdentity_SportStackerIdentityId",
                        column: x => x.SportStackerIdentityId,
                        principalSchema: "dbo",
                        principalTable: "SportStackerIdentity",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StackerIdentityLink_Stacker_StackerId",
                        column: x => x.StackerId,
                        principalSchema: "dbo",
                        principalTable: "Stacker",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_SportStackerIdentity_NadiTrackId",
                schema: "dbo",
                table: "SportStackerIdentity",
                column: "NadiTrackId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StackerIdentityLink_SportStackerIdentityId",
                schema: "dbo",
                table: "StackerIdentityLink",
                column: "SportStackerIdentityId");

            migrationBuilder.CreateIndex(
                name: "UX_StackerIdentityLink_StackerId",
                schema: "dbo",
                table: "StackerIdentityLink",
                column: "StackerId",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StackerIdentityLink",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "SportStackerIdentity",
                schema: "dbo");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Gravelio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPrivacySettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserPrivacySettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    DefaultActivityVisibility = table.Column<int>(type: "integer", nullable: false),
                    HideActivityStartTime = table.Column<bool>(type: "boolean", nullable: false),
                    HideActivityLocation = table.Column<bool>(type: "boolean", nullable: false),
                    HideActivityMap = table.Column<bool>(type: "boolean", nullable: false),
                    HideActivityHr = table.Column<bool>(type: "boolean", nullable: false),
                    HideActivityPower = table.Column<bool>(type: "boolean", nullable: false),
                    HideActivityCadence = table.Column<bool>(type: "boolean", nullable: false),
                    HideActivityElevation = table.Column<bool>(type: "boolean", nullable: false),
                    HideActivitySpeed = table.Column<bool>(type: "boolean", nullable: false),
                    HideActivityPace = table.Column<bool>(type: "boolean", nullable: false),
                    HideActivityGear = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPrivacySettings", x => x.Id);
                    table.CheckConstraint("CK_UserPrivacySettings_Visibility", "\"DefaultActivityVisibility\" BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_UserPrivacySettings_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserPrivacySettings_UserId",
                table: "UserPrivacySettings",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserPrivacySettings");
        }
    }
}

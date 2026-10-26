using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gravelio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDuplicateHandling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Activities_UserId_StartTime",
                table: "Activities");

            migrationBuilder.AddColumn<int>(
                name: "ActivityId",
                table: "Notifications",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActivityName",
                table: "Notifications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DedupKey",
                table: "Activities",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsHidden",
                table: "Activities",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Activities_UserId_DedupKey",
                table: "Activities",
                columns: new[] { "UserId", "DedupKey" },
                unique: true,
                filter: "\"DedupKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_UserId_StartTime",
                table: "Activities",
                columns: new[] { "UserId", "StartTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Activities_UserId_DedupKey",
                table: "Activities");

            migrationBuilder.DropIndex(
                name: "IX_Activities_UserId_StartTime",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "ActivityId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "ActivityName",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "DedupKey",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "IsHidden",
                table: "Activities");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_UserId_StartTime",
                table: "Activities",
                columns: new[] { "UserId", "StartTime" },
                unique: true);
        }
    }
}

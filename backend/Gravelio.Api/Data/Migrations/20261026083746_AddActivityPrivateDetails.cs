using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gravelio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityPrivateDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HiddenDetails",
                table: "Activities",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrivateNotes",
                table: "Activities",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WorkoutFeeling",
                table: "Activities",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WorkoutRpe",
                table: "Activities",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Activities_WorkoutFeeling",
                table: "Activities",
                sql: "\"WorkoutFeeling\" BETWEEN 0 AND 100");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Activities_WorkoutRpe",
                table: "Activities",
                sql: "\"WorkoutRpe\" BETWEEN 10 AND 100");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Activities_WorkoutFeeling",
                table: "Activities");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Activities_WorkoutRpe",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "HiddenDetails",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "PrivateNotes",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "WorkoutFeeling",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "WorkoutRpe",
                table: "Activities");
        }
    }
}

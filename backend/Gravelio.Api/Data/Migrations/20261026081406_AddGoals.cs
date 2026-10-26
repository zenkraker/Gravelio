using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Gravelio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGoals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Goals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Interval = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ActivityGroup = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Metric = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Target = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Goals", x => x.Id);
                    table.CheckConstraint("CK_Goals_ActivityGroup", "\"ActivityGroup\" IN ('run', 'bike', 'swim', 'walk', 'strength', 'cardio')");
                    table.CheckConstraint("CK_Goals_Interval", "\"Interval\" IN ('daily', 'weekly', 'monthly', 'yearly')");
                    table.CheckConstraint("CK_Goals_Metric", "\"Metric\" IN ('calories', 'activities', 'distance', 'elevation', 'duration')");
                    table.CheckConstraint("CK_Goals_Target", "\"Target\" > 0");
                    table.ForeignKey(
                        name: "FK_Goals_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Goals_UserId_Interval_ActivityGroup_Metric",
                table: "Goals",
                columns: new[] { "UserId", "Interval", "ActivityGroup", "Metric" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Goals");
        }
    }
}

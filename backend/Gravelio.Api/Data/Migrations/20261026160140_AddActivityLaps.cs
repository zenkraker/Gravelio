using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Gravelio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityLaps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActivityLaps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ActivityId = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartLat = table.Column<double>(type: "double precision", nullable: true),
                    StartLon = table.Column<double>(type: "double precision", nullable: true),
                    EndLat = table.Column<double>(type: "double precision", nullable: true),
                    EndLon = table.Column<double>(type: "double precision", nullable: true),
                    TotalElapsedTime = table.Column<double>(type: "double precision", nullable: false),
                    TotalTimerTime = table.Column<double>(type: "double precision", nullable: true),
                    Distance = table.Column<double>(type: "double precision", nullable: true),
                    AverageHr = table.Column<int>(type: "integer", nullable: true),
                    MaxHr = table.Column<int>(type: "integer", nullable: true),
                    AverageCad = table.Column<int>(type: "integer", nullable: true),
                    MaxCad = table.Column<int>(type: "integer", nullable: true),
                    AveragePower = table.Column<int>(type: "integer", nullable: true),
                    MaxPower = table.Column<int>(type: "integer", nullable: true),
                    ElevationGain = table.Column<int>(type: "integer", nullable: true),
                    ElevationLoss = table.Column<int>(type: "integer", nullable: true),
                    AverageSpeed = table.Column<double>(type: "double precision", nullable: true),
                    MaxSpeed = table.Column<double>(type: "double precision", nullable: true),
                    Calories = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityLaps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivityLaps_Activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "Activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLaps_ActivityId_Index",
                table: "ActivityLaps",
                columns: new[] { "ActivityId", "Index" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityLaps");
        }
    }
}

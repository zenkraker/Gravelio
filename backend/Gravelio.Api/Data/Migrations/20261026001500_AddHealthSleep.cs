using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Gravelio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHealthSleep : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HealthSleeps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    SleepStartTimeGmt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SleepEndTimeGmt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SleepStartTimeLocal = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SleepEndTimeLocal = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TotalSleepSeconds = table.Column<int>(type: "integer", nullable: true),
                    NapTimeSeconds = table.Column<int>(type: "integer", nullable: true),
                    UnmeasurableSleepSeconds = table.Column<int>(type: "integer", nullable: true),
                    DeepSleepSeconds = table.Column<int>(type: "integer", nullable: true),
                    LightSleepSeconds = table.Column<int>(type: "integer", nullable: true),
                    RemSleepSeconds = table.Column<int>(type: "integer", nullable: true),
                    AwakeSleepSeconds = table.Column<int>(type: "integer", nullable: true),
                    AvgHeartRate = table.Column<int>(type: "integer", nullable: true),
                    MinHeartRate = table.Column<int>(type: "integer", nullable: true),
                    MaxHeartRate = table.Column<int>(type: "integer", nullable: true),
                    AvgSpo2 = table.Column<int>(type: "integer", nullable: true),
                    LowestSpo2 = table.Column<int>(type: "integer", nullable: true),
                    HighestSpo2 = table.Column<int>(type: "integer", nullable: true),
                    AvgRespiration = table.Column<int>(type: "integer", nullable: true),
                    LowestRespiration = table.Column<int>(type: "integer", nullable: true),
                    HighestRespiration = table.Column<int>(type: "integer", nullable: true),
                    AvgStressLevel = table.Column<int>(type: "integer", nullable: true),
                    AwakeCount = table.Column<int>(type: "integer", nullable: true),
                    RestlessMomentsCount = table.Column<int>(type: "integer", nullable: true),
                    SleepScoreOverall = table.Column<int>(type: "integer", nullable: true),
                    SleepScoreDuration = table.Column<string>(type: "text", nullable: true),
                    SleepScoreQuality = table.Column<string>(type: "text", nullable: true),
                    GarminconnectSleepId = table.Column<string>(type: "text", nullable: true),
                    SleepStages = table.Column<string>(type: "jsonb", nullable: true),
                    Source = table.Column<string>(type: "text", nullable: true),
                    HrvStatus = table.Column<string>(type: "text", nullable: true),
                    RestingHeartRate = table.Column<int>(type: "integer", nullable: true),
                    AvgSkinTempDeviation = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    AwakeCountScore = table.Column<string>(type: "text", nullable: true),
                    RemPercentageScore = table.Column<string>(type: "text", nullable: true),
                    DeepPercentageScore = table.Column<string>(type: "text", nullable: true),
                    LightPercentageScore = table.Column<string>(type: "text", nullable: true),
                    AvgSleepStress = table.Column<int>(type: "integer", nullable: true),
                    SleepStressScore = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HealthSleeps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HealthSleeps_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HealthSleeps_UserId_Date",
                table: "HealthSleeps",
                columns: new[] { "UserId", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HealthSleeps");
        }
    }
}

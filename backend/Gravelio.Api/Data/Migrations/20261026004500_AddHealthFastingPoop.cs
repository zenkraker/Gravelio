using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Gravelio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHealthFastingPoop : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HealthFastings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    FastStartTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FastEndTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TargetDurationSeconds = table.Column<int>(type: "integer", nullable: true),
                    ActualDurationSeconds = table.Column<int>(type: "integer", nullable: true),
                    FastingType = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    Source = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HealthFastings", x => x.Id);
                    table.CheckConstraint("CK_HealthFastings_Status", "\"Status\" IN ('in_progress', 'completed', 'broken', 'cancelled')");
                    table.ForeignKey(
                        name: "FK_HealthFastings_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HealthPoops",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    DateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BristolType = table.Column<int>(type: "integer", nullable: true),
                    Color = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    Source = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HealthPoops", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HealthPoops_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HealthFastings_UserId",
                table: "HealthFastings",
                column: "UserId",
                unique: true,
                filter: "\"Status\" = 'in_progress'");

            migrationBuilder.CreateIndex(
                name: "IX_HealthFastings_UserId_FastStartTime",
                table: "HealthFastings",
                columns: new[] { "UserId", "FastStartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_HealthPoops_UserId_DateTime",
                table: "HealthPoops",
                columns: new[] { "UserId", "DateTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HealthFastings");

            migrationBuilder.DropTable(
                name: "HealthPoops");
        }
    }
}

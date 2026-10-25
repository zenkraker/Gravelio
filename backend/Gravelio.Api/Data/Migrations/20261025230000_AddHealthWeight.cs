using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Gravelio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHealthWeight : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HealthWeights",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Weight = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Bmi = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    BodyFat = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    BodyWater = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    BoneMass = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    MuscleMass = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    PhysiqueRating = table.Column<int>(type: "integer", nullable: true),
                    VisceralFat = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    MetabolicAge = table.Column<int>(type: "integer", nullable: true),
                    Source = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HealthWeights", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HealthWeights_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HealthWeights_UserId_Date",
                table: "HealthWeights",
                columns: new[] { "UserId", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HealthWeights");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Gravelio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGear : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "AspNetUsers",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "GearId",
                table: "Activities",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Gears",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Nickname = table.Column<string>(type: "text", nullable: false),
                    Brand = table.Column<string>(type: "text", nullable: true),
                    Model = table.Column<string>(type: "text", nullable: true),
                    GearType = table.Column<int>(type: "integer", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    InitialDistance = table.Column<double>(type: "double precision", nullable: false),
                    PurchaseValue = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Gears", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Gears_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DefaultGears",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    ActivityType = table.Column<int>(type: "integer", nullable: false),
                    GearId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DefaultGears", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DefaultGears_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DefaultGears_Gears_GearId",
                        column: x => x.GearId,
                        principalTable: "Gears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GearComponents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GearId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Brand = table.Column<string>(type: "text", nullable: true),
                    Model = table.Column<string>(type: "text", nullable: true),
                    PurchaseDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RetiredDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    ExpectedDistance = table.Column<double>(type: "double precision", nullable: true),
                    PurchaseValue = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GearComponents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GearComponents_Gears_GearId",
                        column: x => x.GearId,
                        principalTable: "Gears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Activities_GearId",
                table: "Activities",
                column: "GearId");

            migrationBuilder.CreateIndex(
                name: "IX_DefaultGears_GearId",
                table: "DefaultGears",
                column: "GearId");

            migrationBuilder.CreateIndex(
                name: "IX_DefaultGears_UserId_ActivityType",
                table: "DefaultGears",
                columns: new[] { "UserId", "ActivityType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GearComponents_GearId",
                table: "GearComponents",
                column: "GearId");

            migrationBuilder.CreateIndex(
                name: "IX_Gears_UserId_Nickname",
                table: "Gears",
                columns: new[] { "UserId", "Nickname" });

            migrationBuilder.AddForeignKey(
                name: "FK_Activities_Gears_GearId",
                table: "Activities",
                column: "GearId",
                principalTable: "Gears",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Activities_Gears_GearId",
                table: "Activities");

            migrationBuilder.DropTable(
                name: "DefaultGears");

            migrationBuilder.DropTable(
                name: "GearComponents");

            migrationBuilder.DropTable(
                name: "Gears");

            migrationBuilder.DropIndex(
                name: "IX_Activities_GearId",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "GearId",
                table: "Activities");
        }
    }
}

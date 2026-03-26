using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIvisiontest.Migrations
{
    /// <inheritdoc />
    public partial class InitWaferInspection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Equipments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EquipmentCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EquipmentName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true, defaultValue: "Online"),
                    LastMaintenanceAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Equipments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Lots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LotNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProductModel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WaferInspectionResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WaferId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SlotNumber = table.Column<int>(type: "int", nullable: true),
                    LotId = table.Column<int>(type: "int", nullable: false),
                    EquipmentId = table.Column<int>(type: "int", nullable: false),
                    InspectedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    DurationSeconds = table.Column<double>(type: "float", nullable: true),
                    Operator = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsPassed = table.Column<bool>(type: "bit", nullable: false),
                    DefectCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    DefectTypes = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DefectDensity = table.Column<double>(type: "float", nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaferInspectionResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WaferInspectionResults_Equipments_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WaferInspectionResults_Lots_LotId",
                        column: x => x.LotId,
                        principalTable: "Lots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Equipments_EquipmentCode",
                table: "Equipments",
                column: "EquipmentCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Lots_LotNumber",
                table: "Lots",
                column: "LotNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WaferInspectionResults_EquipmentId",
                table: "WaferInspectionResults",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_WaferInspectionResults_InspectedAt",
                table: "WaferInspectionResults",
                column: "InspectedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WaferInspectionResults_IsPassed",
                table: "WaferInspectionResults",
                column: "IsPassed");

            migrationBuilder.CreateIndex(
                name: "IX_WaferInspectionResults_LotId",
                table: "WaferInspectionResults",
                column: "LotId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WaferInspectionResults");

            migrationBuilder.DropTable(
                name: "Equipments");

            migrationBuilder.DropTable(
                name: "Lots");
        }
    }
}

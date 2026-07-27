using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LivingRealms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPersistentCampaignPhases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PhaseRound",
                schema: "living_realms",
                table: "StonehavenAssaults",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "InitialStructureStrength",
                schema: "living_realms",
                table: "SettlementRaids",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Phase",
                schema: "living_realms",
                table: "SettlementRaids",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PhaseRound",
                schema: "living_realms",
                table: "SettlementRaids",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StructureStrength",
                schema: "living_realms",
                table: "SettlementRaids",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                UPDATE living_realms."SettlementRaids"
                SET "Phase" = 4
                WHERE "Status" IN (2, 3, 4);

                UPDATE living_realms."SettlementRaids"
                SET "Phase" = 2,
                    "InitialStructureStrength" = (
                        SELECT COALESCE(SUM("Health"), 0)
                        FROM living_realms."WorldStructures"
                        WHERE "Owner" = 0
                    ),
                    "StructureStrength" = (
                        SELECT COALESCE(SUM("Health"), 0)
                        FROM living_realms."WorldStructures"
                        WHERE "Owner" = 0
                    )
                WHERE "Status" = 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhaseRound",
                schema: "living_realms",
                table: "StonehavenAssaults");

            migrationBuilder.DropColumn(
                name: "InitialStructureStrength",
                schema: "living_realms",
                table: "SettlementRaids");

            migrationBuilder.DropColumn(
                name: "Phase",
                schema: "living_realms",
                table: "SettlementRaids");

            migrationBuilder.DropColumn(
                name: "PhaseRound",
                schema: "living_realms",
                table: "SettlementRaids");

            migrationBuilder.DropColumn(
                name: "StructureStrength",
                schema: "living_realms",
                table: "SettlementRaids");
        }
    }
}

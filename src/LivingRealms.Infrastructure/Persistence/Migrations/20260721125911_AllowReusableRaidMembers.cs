using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LivingRealms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowReusableRaidMembers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SettlementRaidAttackers_CreatureId",
                schema: "living_realms",
                table: "SettlementRaidAttackers");

            migrationBuilder.CreateIndex(
                name: "IX_SettlementRaidAttackers_CreatureId",
                schema: "living_realms",
                table: "SettlementRaidAttackers",
                column: "CreatureId");

            migrationBuilder.CreateIndex(
                name: "IX_SettlementRaidAttackers_RaidId_CreatureId",
                schema: "living_realms",
                table: "SettlementRaidAttackers",
                columns: new[] { "RaidId", "CreatureId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SettlementRaidAttackers_CreatureId",
                schema: "living_realms",
                table: "SettlementRaidAttackers");

            migrationBuilder.DropIndex(
                name: "IX_SettlementRaidAttackers_RaidId_CreatureId",
                schema: "living_realms",
                table: "SettlementRaidAttackers");

            migrationBuilder.CreateIndex(
                name: "IX_SettlementRaidAttackers_CreatureId",
                schema: "living_realms",
                table: "SettlementRaidAttackers",
                column: "CreatureId",
                unique: true);
        }
    }
}

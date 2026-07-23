using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LivingRealms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase7BStonehavenRaids : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SettlementRaids",
                schema: "living_realms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SettlementId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttackingFactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    WorldDay = table.Column<int>(type: "integer", nullable: false),
                    ScheduledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastAdvancedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    InitialAttackerStrength = table.Column<int>(type: "integer", nullable: false),
                    AttackerStrength = table.Column<int>(type: "integer", nullable: false),
                    InitialDefenderStrength = table.Column<int>(type: "integer", nullable: false),
                    DefenderStrength = table.Column<int>(type: "integer", nullable: false),
                    PlayerContribution = table.Column<int>(type: "integer", nullable: false),
                    SettlementDamage = table.Column<int>(type: "integer", nullable: false),
                    ResidentCasualties = table.Column<int>(type: "integer", nullable: false),
                    ResidentInjuries = table.Column<int>(type: "integer", nullable: false),
                    OutcomeSummary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SettlementRaids", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SettlementRaids_Factions_AttackingFactionId",
                        column: x => x.AttackingFactionId,
                        principalSchema: "living_realms",
                        principalTable: "Factions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SettlementRaids_Settlements_SettlementId",
                        column: x => x.SettlementId,
                        principalSchema: "living_realms",
                        principalTable: "Settlements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SettlementRaidAttackers",
                schema: "living_realms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RaidId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatureId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDefeated = table.Column<bool>(type: "boolean", nullable: false),
                    DefeatedByCharacterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefeatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SettlementRaidAttackers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SettlementRaidAttackers_Characters_DefeatedByCharacterId",
                        column: x => x.DefeatedByCharacterId,
                        principalSchema: "living_realms",
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SettlementRaidAttackers_Creatures_CreatureId",
                        column: x => x.CreatureId,
                        principalSchema: "living_realms",
                        principalTable: "Creatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SettlementRaidAttackers_SettlementRaids_RaidId",
                        column: x => x.RaidId,
                        principalSchema: "living_realms",
                        principalTable: "SettlementRaids",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SettlementRaidAttackers_CreatureId",
                schema: "living_realms",
                table: "SettlementRaidAttackers",
                column: "CreatureId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SettlementRaidAttackers_DefeatedByCharacterId",
                schema: "living_realms",
                table: "SettlementRaidAttackers",
                column: "DefeatedByCharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_SettlementRaidAttackers_RaidId_IsDefeated",
                schema: "living_realms",
                table: "SettlementRaidAttackers",
                columns: new[] { "RaidId", "IsDefeated" });

            migrationBuilder.CreateIndex(
                name: "IX_SettlementRaids_AttackingFactionId_Status",
                schema: "living_realms",
                table: "SettlementRaids",
                columns: new[] { "AttackingFactionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SettlementRaids_SettlementId_Status_ScheduledAt",
                schema: "living_realms",
                table: "SettlementRaids",
                columns: new[] { "SettlementId", "Status", "ScheduledAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SettlementRaidAttackers",
                schema: "living_realms");

            migrationBuilder.DropTable(
                name: "SettlementRaids",
                schema: "living_realms");
        }
    }
}

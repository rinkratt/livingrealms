using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LivingRealms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase5IronEconomy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ArmorTier",
                schema: "living_realms",
                table: "Settlements",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LastMineGuardWageDay",
                schema: "living_realms",
                table: "Settlements",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MineGuardCount",
                schema: "living_realms",
                table: "Settlements",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TreasuryGold",
                schema: "living_realms",
                table: "Settlements",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WeaponTier",
                schema: "living_realms",
                table: "Settlements",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ArmorTier",
                schema: "living_realms",
                table: "Factions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WeaponTier",
                schema: "living_realms",
                table: "Factions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "IronMiningOperations",
                schema: "living_realms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Owner = table.Column<int>(type: "integer", nullable: false),
                    MinerName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ResidentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatureId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PositionX = table.Column<float>(type: "real", nullable: false),
                    PositionY = table.Column<float>(type: "real", nullable: false),
                    PositionZ = table.Column<float>(type: "real", nullable: false),
                    CargoIron = table.Column<int>(type: "integer", nullable: false),
                    TotalIronDelivered = table.Column<int>(type: "integer", nullable: false),
                    TripsCompleted = table.Column<int>(type: "integer", nullable: false),
                    LastTransitionAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IronMiningOperations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IronMiningOperations_Creatures_CreatureId",
                        column: x => x.CreatureId,
                        principalSchema: "living_realms",
                        principalTable: "Creatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_IronMiningOperations_SettlementResidents_ResidentId",
                        column: x => x.ResidentId,
                        principalSchema: "living_realms",
                        principalTable: "SettlementResidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "Factions",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-4000-8000-000000000001"),
                columns: new[] { "ArmorTier", "WeaponTier" },
                values: new object[] { 0, 0 });

            migrationBuilder.InsertData(
                schema: "living_realms",
                table: "IronMiningOperations",
                columns: new[] { "Id", "CargoIron", "CreatedAt", "CreatureId", "LastTransitionAt", "MinerName", "Owner", "PositionX", "PositionY", "PositionZ", "ResidentId", "Status", "TotalIronDelivered", "TripsCompleted", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("84000000-0000-4000-8000-000000000001"), 0, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Dain", 0, 7f, 0.08f, -24f, new Guid("70000000-0000-4000-8000-000000000009"), 0, 0, 0, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("84000000-0000-4000-8000-000000000002"), 0, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Darkwood miner not yet assigned", 1, -116f, 0.08f, -104f, null, 0, 0, 0, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) }
                });

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "SettlementResidents",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-4000-8000-000000000009"),
                columns: new[] { "Dialogue", "MemorySummary", "PrimarySkill", "Role", "WorkX", "WorkZ" },
                values: new object[] { "Every ingot begins at Irondeep. I walk the ore home before Brann counts it.", "Dain works the only known iron vein in A3 and records every load delivered to Stonehaven.", "Iron Mining", "Iron Miner", 121f, -103f });

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "Settlements",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-4000-8000-000000000001"),
                columns: new[] { "ArmorTier", "LastMineGuardWageDay", "MineGuardCount", "TreasuryGold", "WeaponTier" },
                values: new object[] { 0, 0, 0, 30, 0 });

            migrationBuilder.CreateIndex(
                name: "IX_IronMiningOperations_CreatureId",
                schema: "living_realms",
                table: "IronMiningOperations",
                column: "CreatureId");

            migrationBuilder.CreateIndex(
                name: "IX_IronMiningOperations_Owner",
                schema: "living_realms",
                table: "IronMiningOperations",
                column: "Owner",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IronMiningOperations_ResidentId",
                schema: "living_realms",
                table: "IronMiningOperations",
                column: "ResidentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IronMiningOperations",
                schema: "living_realms");

            migrationBuilder.DropColumn(
                name: "ArmorTier",
                schema: "living_realms",
                table: "Settlements");

            migrationBuilder.DropColumn(
                name: "LastMineGuardWageDay",
                schema: "living_realms",
                table: "Settlements");

            migrationBuilder.DropColumn(
                name: "MineGuardCount",
                schema: "living_realms",
                table: "Settlements");

            migrationBuilder.DropColumn(
                name: "TreasuryGold",
                schema: "living_realms",
                table: "Settlements");

            migrationBuilder.DropColumn(
                name: "WeaponTier",
                schema: "living_realms",
                table: "Settlements");

            migrationBuilder.DropColumn(
                name: "ArmorTier",
                schema: "living_realms",
                table: "Factions");

            migrationBuilder.DropColumn(
                name: "WeaponTier",
                schema: "living_realms",
                table: "Factions");

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "SettlementResidents",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-4000-8000-000000000009"),
                columns: new[] { "Dialogue", "MemorySummary", "PrimarySkill", "Role", "WorkX", "WorkZ" },
                values: new object[] { "Stonehaven's walls begin in the quarry. Give me a strong back and enough daylight.", "Dain marks every stone load so the wall ledger can explain where its strength came from.", "Quarrying", "Quarry Worker", 88f, -96f });
        }
    }
}

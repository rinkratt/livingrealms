using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LivingRealms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConnectedRegionsAndNaturalSettlementGrowth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "ConstructionProjects",
                keyColumn: "Id",
                keyValue: new Guid("81000000-0000-4000-8000-000000000004"),
                columns: new[] { "PositionX", "PositionZ" },
                values: new object[] { 88f, -91f });

            migrationBuilder.InsertData(
                schema: "living_realms",
                table: "Items",
                columns: new[] { "Id", "AttackBonus", "BaseValue", "CreatedAt", "DefenseBonus", "Description", "EquipmentSlot", "HealingAmount", "Key", "Kind", "Name", "Rarity", "RequiredArchetype", "UnitWeight", "UpdatedAt" },
                values: new object[] { new Guid("105a7b69-0e17-40d0-8d0f-4aa63bfb1013"), 0, 7, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0, "Dense ore from Irondeep Mine. Brann and Oren both need dependable local iron.", null, 0, "raw-iron-ore", 4, "Raw Iron Ore", 1, null, 3, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "SettlementResidents",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-4000-8000-000000000009"),
                columns: new[] { "WorkX", "WorkZ" },
                values: new object[] { 88f, -96f });

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "Settlements",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-4000-8000-000000000001"),
                columns: new[] { "Food", "Iron", "Population", "Stone", "Wood" },
                values: new object[] { 64, 4, 8, 24, 40 });

            migrationBuilder.Sql(
                """
                UPDATE living_realms."SettlementResidents"
                SET "Status" = 2,
                    "Health" = "MaximumHealth",
                    "UpdatedAt" = NOW()
                WHERE "SettlementId" = '40000000-0000-4000-8000-000000000001'
                  AND "Id" NOT IN (
                    '70000000-0000-4000-8000-000000000001',
                    '70000000-0000-4000-8000-000000000002',
                    '70000000-0000-4000-8000-000000000003',
                    '70000000-0000-4000-8000-000000000004',
                    '70000000-0000-4000-8000-000000000005',
                    '70000000-0000-4000-8000-000000000006',
                    '70000000-0000-4000-8000-000000000007',
                    '70000000-0000-4000-8000-000000000008',
                    '70000000-0000-4000-8000-000000000009'
                  );

                UPDATE living_realms."SettlementResidents"
                SET "Status" = CASE
                        WHEN "Id" = '70000000-0000-4000-8000-000000000005' THEN 2
                        ELSE 0
                    END,
                    "Health" = "MaximumHealth",
                    "UpdatedAt" = NOW()
                WHERE "SettlementId" = '40000000-0000-4000-8000-000000000001'
                  AND "Id" IN (
                    '70000000-0000-4000-8000-000000000001',
                    '70000000-0000-4000-8000-000000000002',
                    '70000000-0000-4000-8000-000000000003',
                    '70000000-0000-4000-8000-000000000004',
                    '70000000-0000-4000-8000-000000000005',
                    '70000000-0000-4000-8000-000000000006',
                    '70000000-0000-4000-8000-000000000007',
                    '70000000-0000-4000-8000-000000000008',
                    '70000000-0000-4000-8000-000000000009'
                  );
                """);

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "WorldResourceNodes",
                keyColumn: "Id",
                keyValue: new Guid("82000000-0000-4000-8000-000000000003"),
                columns: new[] { "Name", "PositionX", "PositionZ" },
                values: new object[] { "Irondeep Quarry Face", 88f, -96f });

            migrationBuilder.InsertData(
                schema: "living_realms",
                table: "WorldResourceNodes",
                columns: new[] { "Id", "Capacity", "CreatedAt", "Key", "Kind", "Name", "Owner", "PositionX", "PositionY", "PositionZ", "RegionId", "Remaining", "RespawnAt", "RespawnSeconds", "UpdatedAt", "YieldPerHarvest" },
                values: new object[] { new Guid("82000000-0000-4000-8000-000000000008"), 45, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "irondeep-ore-vein", 3, "Irondeep Ore Vein", 0, 121f, 0.08f, -103f, new Guid("7139a553-cea3-45e4-9d91-b3a95629b72e"), 45, null, 150, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 3 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "living_realms",
                table: "Items",
                keyColumn: "Id",
                keyValue: new Guid("105a7b69-0e17-40d0-8d0f-4aa63bfb1013"));

            migrationBuilder.DeleteData(
                schema: "living_realms",
                table: "WorldResourceNodes",
                keyColumn: "Id",
                keyValue: new Guid("82000000-0000-4000-8000-000000000008"));

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "ConstructionProjects",
                keyColumn: "Id",
                keyValue: new Guid("81000000-0000-4000-8000-000000000004"),
                columns: new[] { "PositionX", "PositionZ" },
                values: new object[] { 34f, 30f });

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "SettlementResidents",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-4000-8000-000000000009"),
                columns: new[] { "WorkX", "WorkZ" },
                values: new object[] { 36f, 30f });

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "Settlements",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-4000-8000-000000000001"),
                columns: new[] { "Food", "Iron", "Population", "Stone", "Wood" },
                values: new object[] { 420, 35, 8, 120, 180 });

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "WorldResourceNodes",
                keyColumn: "Id",
                keyValue: new Guid("82000000-0000-4000-8000-000000000003"),
                columns: new[] { "Name", "PositionX", "PositionZ" },
                values: new object[] { "East Quarry Face", 39f, 30f });
        }
    }
}

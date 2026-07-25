using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LivingRealms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RepairRegionalPlaytestAlignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "SettlementResidents",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-4000-8000-000000000008"),
                columns: new[] { "WorkX", "WorkZ" },
                values: new object[] { -27.3f, -19.5f });

            migrationBuilder.Sql(
                """
                UPDATE living_realms."Creatures" AS creature
                SET
                    "PositionX" = repaired.x,
                    "PositionY" = 0.08,
                    "PositionZ" = repaired.z,
                    "SpawnX" = repaired.x,
                    "SpawnY" = 0.08,
                    "SpawnZ" = repaired.z,
                    "Health" = creature."MaximumHealth",
                    "Status" = 0,
                    "RespawnAt" = NULL,
                    "LastAttackAt" = NULL,
                    "UpdatedAt" = NOW()
                FROM (
                    VALUES
                        ('8bd3a92f-80a8-46a6-8349-427975490a01'::uuid, 76.0::real, 68.0::real),
                        ('8bd3a92f-80a8-46a6-8349-427975490a02'::uuid, 92.0::real, 78.0::real),
                        ('8bd3a92f-80a8-46a6-8349-427975490a03'::uuid, 110.0::real, 72.0::real),
                        ('5d8a9637-a327-4f42-8ec3-a292f548d101'::uuid, 84.0::real, 101.0::real),
                        ('5d8a9637-a327-4f42-8ec3-a292f548d102'::uuid, 111.0::real, 105.0::real)
                ) AS repaired(id, x, z)
                WHERE creature."Id" = repaired.id;

                UPDATE living_realms."Factions"
                SET
                    "Population" = GREATEST("Population", 7),
                    "PopulationCapacity" = GREATEST("PopulationCapacity", 10),
                    "UpdatedAt" = NOW()
                WHERE "Id" = '50000000-0000-4000-8000-000000000001'::uuid;

                UPDATE living_realms."Creatures" AS creature
                SET
                    "PositionX" = creature."SpawnX",
                    "PositionY" = GREATEST(creature."SpawnY", 0.08),
                    "PositionZ" = creature."SpawnZ",
                    "UpdatedAt" = NOW()
                WHERE creature."FactionId" = '50000000-0000-4000-8000-000000000001'::uuid
                  AND creature."Status" = 0
                  AND creature."Health" > 0
                  AND NOT EXISTS (
                      SELECT 1
                      FROM living_realms."SettlementRaidAttackers" AS attacker
                      INNER JOIN living_realms."SettlementRaids" AS raid
                          ON raid."Id" = attacker."RaidId"
                      WHERE attacker."CreatureId" = creature."Id"
                        AND raid."Status" IN (0, 1)
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "SettlementResidents",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-4000-8000-000000000008"),
                columns: new[] { "WorkX", "WorkZ" },
                values: new object[] { -36.5f, -18f });
        }
    }
}

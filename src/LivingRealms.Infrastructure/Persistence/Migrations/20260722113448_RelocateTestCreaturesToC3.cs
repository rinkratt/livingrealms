using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LivingRealms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RelocateTestCreaturesToC3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "Creatures",
                keyColumn: "Id",
                keyValue: new Guid("5d8a9637-a327-4f42-8ec3-a292f548d101"),
                columns: new[] { "PositionX", "PositionZ", "SpawnX", "SpawnZ" },
                values: new object[] { 84f, 101f, 84f, 101f });

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "Creatures",
                keyColumn: "Id",
                keyValue: new Guid("5d8a9637-a327-4f42-8ec3-a292f548d102"),
                columns: new[] { "PositionX", "PositionZ", "SpawnX", "SpawnZ" },
                values: new object[] { 111f, 105f, 111f, 105f });

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "Creatures",
                keyColumn: "Id",
                keyValue: new Guid("8bd3a92f-80a8-46a6-8349-427975490a01"),
                columns: new[] { "PositionX", "PositionZ", "SpawnX", "SpawnZ" },
                values: new object[] { 76f, 68f, 76f, 68f });

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "Creatures",
                keyColumn: "Id",
                keyValue: new Guid("8bd3a92f-80a8-46a6-8349-427975490a02"),
                columns: new[] { "PositionX", "PositionZ", "SpawnX", "SpawnZ" },
                values: new object[] { 92f, 78f, 92f, 78f });

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "Creatures",
                keyColumn: "Id",
                keyValue: new Guid("8bd3a92f-80a8-46a6-8349-427975490a03"),
                columns: new[] { "PositionX", "PositionZ", "SpawnX", "SpawnZ" },
                values: new object[] { 110f, 72f, 110f, 72f });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "Creatures",
                keyColumn: "Id",
                keyValue: new Guid("5d8a9637-a327-4f42-8ec3-a292f548d101"),
                columns: new[] { "PositionX", "PositionZ", "SpawnX", "SpawnZ" },
                values: new object[] { -29f, 12f, -29f, 12f });

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "Creatures",
                keyColumn: "Id",
                keyValue: new Guid("5d8a9637-a327-4f42-8ec3-a292f548d102"),
                columns: new[] { "PositionX", "PositionZ", "SpawnX", "SpawnZ" },
                values: new object[] { 29f, 16f, 29f, 16f });

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "Creatures",
                keyColumn: "Id",
                keyValue: new Guid("8bd3a92f-80a8-46a6-8349-427975490a01"),
                columns: new[] { "PositionX", "PositionZ", "SpawnX", "SpawnZ" },
                values: new object[] { -16f, 17f, -16f, 17f });

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "Creatures",
                keyColumn: "Id",
                keyValue: new Guid("8bd3a92f-80a8-46a6-8349-427975490a02"),
                columns: new[] { "PositionX", "PositionZ", "SpawnX", "SpawnZ" },
                values: new object[] { -8f, 34f, -8f, 34f });

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "Creatures",
                keyColumn: "Id",
                keyValue: new Guid("8bd3a92f-80a8-46a6-8349-427975490a03"),
                columns: new[] { "PositionX", "PositionZ", "SpawnX", "SpawnZ" },
                values: new object[] { 12f, 32f, 12f, 32f });
        }
    }
}

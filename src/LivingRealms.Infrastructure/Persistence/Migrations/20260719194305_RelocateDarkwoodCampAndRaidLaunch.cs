using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LivingRealms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RelocateDarkwoodCampAndRaidLaunch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "Creatures",
                keyColumn: "Id",
                keyValue: new Guid("9230414d-a60d-46ca-9c59-36cc3b867201"),
                columns: new[] { "PositionX", "PositionZ", "SpawnX", "SpawnZ" },
                values: new object[] { -124f, -99f, -124f, -99f });

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "Creatures",
                keyColumn: "Id",
                keyValue: new Guid("9230414d-a60d-46ca-9c59-36cc3b867202"),
                columns: new[] { "PositionX", "PositionZ", "SpawnX", "SpawnZ" },
                values: new object[] { -107f, -103f, -107f, -103f });

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "Creatures",
                keyColumn: "Id",
                keyValue: new Guid("f4c5a7b9-644f-4c85-b18f-ac38294e3001"),
                columns: new[] { "PositionX", "PositionZ", "SpawnX", "SpawnZ" },
                values: new object[] { -116f, -112f, -116f, -112f });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "Creatures",
                keyColumn: "Id",
                keyValue: new Guid("9230414d-a60d-46ca-9c59-36cc3b867201"),
                columns: new[] { "PositionX", "PositionZ", "SpawnX", "SpawnZ" },
                values: new object[] { -23f, -39f, -23f, -39f });

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "Creatures",
                keyColumn: "Id",
                keyValue: new Guid("9230414d-a60d-46ca-9c59-36cc3b867202"),
                columns: new[] { "PositionX", "PositionZ", "SpawnX", "SpawnZ" },
                values: new object[] { 22f, -39f, 22f, -39f });

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "Creatures",
                keyColumn: "Id",
                keyValue: new Guid("f4c5a7b9-644f-4c85-b18f-ac38294e3001"),
                columns: new[] { "PositionX", "PositionZ", "SpawnX", "SpawnZ" },
                values: new object[] { 0f, -41f, 0f, -41f });
        }
    }
}

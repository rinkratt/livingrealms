using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LivingRealms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase6OfflineWorldProgression : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "SimulatedHours",
                schema: "living_realms",
                table: "Factions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.InsertData(
                schema: "living_realms",
                table: "Factions",
                columns: new[] { "Id", "Aggression", "CreatedAt", "DevelopmentStage", "Key", "LastProcessedAt", "LeaderCreatureId", "MilitaryStrength", "Morale", "Name", "NextDecisionAt", "Population", "PopulationCapacity", "SimulatedHours", "TechnologyLevel", "TerritorySize", "UpdatedAt" },
                values: new object[] { new Guid("50000000-0000-4000-8000-000000000001"), 45, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1, "darkwood-clan", new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("f4c5a7b9-644f-4c85-b18f-ac38294e3001"), 60, 55, "Darkwood Clan", new DateTimeOffset(new DateTime(2026, 7, 17, 15, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6, 10, 0L, 1, 1, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) });

            // Offline progression starts at installation time, not at build time.
            migrationBuilder.Sql(
                """
                UPDATE living_realms."Factions"
                SET "LastProcessedAt" = CURRENT_TIMESTAMP,
                    "NextDecisionAt" = CURRENT_TIMESTAMP + INTERVAL '1 hour',
                    "UpdatedAt" = CURRENT_TIMESTAMP
                WHERE "Id" = '50000000-0000-4000-8000-000000000001';
                """);

            migrationBuilder.InsertData(
                schema: "living_realms",
                table: "Settlements",
                columns: new[] { "Id", "CreatedAt", "DefenseRating", "Food", "GuardStrength", "Iron", "IsDestroyed", "LastAttackedAt", "Name", "Population", "RegionId", "Stone", "StructuralIntegrity", "UpdatedAt", "Wood" },
                values: new object[] { new Guid("40000000-0000-4000-8000-000000000001"), new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 65, 420, 42, 35, false, null, "Stonehaven Village", 84, new Guid("7139a553-cea3-45e4-9d91-b3a95629b72e"), 120, 1000, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 180 });

            migrationBuilder.InsertData(
                schema: "living_realms",
                table: "WorldHistory",
                columns: new[] { "Id", "CharacterId", "CreatedAt", "CreatureId", "Description", "EventType", "FactionId", "ImportanceLevel", "OccurredAt", "RegionId", "Title", "UpdatedAt" },
                values: new object[] { new Guid("60000000-0000-4000-8000-000000000001"), null, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("f4c5a7b9-644f-4c85-b18f-ac38294e3001"), "Six goblins gathered beneath Gorvak and established a crude encampment beyond Stonehaven's northern road.", "faction_founded", new Guid("50000000-0000-4000-8000-000000000001"), 2, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("7139a553-cea3-45e4-9d91-b3a95629b72e"), "The Darkwood Clan raised its first tents", new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.InsertData(
                schema: "living_realms",
                table: "FactionResources",
                columns: new[] { "Id", "Amount", "Capacity", "CreatedAt", "FactionId", "Kind", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("51000000-0000-4000-8000-000000000001"), 80L, 250L, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("50000000-0000-4000-8000-000000000001"), 0, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("51000000-0000-4000-8000-000000000002"), 50L, 250L, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("50000000-0000-4000-8000-000000000001"), 1, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("51000000-0000-4000-8000-000000000003"), 15L, 180L, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("50000000-0000-4000-8000-000000000001"), 2, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("51000000-0000-4000-8000-000000000004"), 5L, 120L, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("50000000-0000-4000-8000-000000000001"), 3, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("51000000-0000-4000-8000-000000000005"), 0L, 100L, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("50000000-0000-4000-8000-000000000001"), 4, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) }
                });

            migrationBuilder.InsertData(
                schema: "living_realms",
                table: "FactionStructures",
                columns: new[] { "Id", "CompletedAt", "CreatedAt", "FactionId", "Health", "Level", "StructureType", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("52000000-0000-4000-8000-000000000001"), new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("50000000-0000-4000-8000-000000000001"), 100, 1, "Hide Tents", new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("52000000-0000-4000-8000-000000000002"), new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("50000000-0000-4000-8000-000000000001"), 100, 1, "Crude Stockpile", new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) }
                });

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "Creatures",
                keyColumn: "Id",
                keyValue: new Guid("9230414d-a60d-46ca-9c59-36cc3b867201"),
                columns: new[] { "FactionId", "Role" },
                values: new object[] { new Guid("50000000-0000-4000-8000-000000000001"), "Raider" });

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "Creatures",
                keyColumn: "Id",
                keyValue: new Guid("9230414d-a60d-46ca-9c59-36cc3b867202"),
                columns: new[] { "FactionId", "Role" },
                values: new object[] { new Guid("50000000-0000-4000-8000-000000000001"), "Raider" });

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "Creatures",
                keyColumn: "Id",
                keyValue: new Guid("f4c5a7b9-644f-4c85-b18f-ac38294e3001"),
                columns: new[] { "FactionId", "Leadership", "Title" },
                values: new object[] { new Guid("50000000-0000-4000-8000-000000000001"), 10, "Goblin Chief" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "Creatures",
                keyColumn: "Id",
                keyValue: new Guid("9230414d-a60d-46ca-9c59-36cc3b867201"),
                columns: new[] { "FactionId", "Role" },
                values: new object[] { null, "Wild Creature" });

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "Creatures",
                keyColumn: "Id",
                keyValue: new Guid("9230414d-a60d-46ca-9c59-36cc3b867202"),
                columns: new[] { "FactionId", "Role" },
                values: new object[] { null, "Wild Creature" });

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "Creatures",
                keyColumn: "Id",
                keyValue: new Guid("f4c5a7b9-644f-4c85-b18f-ac38294e3001"),
                columns: new[] { "FactionId", "Leadership", "Title" },
                values: new object[] { null, 0, "Clan Chief" });

            migrationBuilder.DeleteData(
                schema: "living_realms",
                table: "FactionResources",
                keyColumn: "Id",
                keyValue: new Guid("51000000-0000-4000-8000-000000000001"));

            migrationBuilder.DeleteData(
                schema: "living_realms",
                table: "FactionResources",
                keyColumn: "Id",
                keyValue: new Guid("51000000-0000-4000-8000-000000000002"));

            migrationBuilder.DeleteData(
                schema: "living_realms",
                table: "FactionResources",
                keyColumn: "Id",
                keyValue: new Guid("51000000-0000-4000-8000-000000000003"));

            migrationBuilder.DeleteData(
                schema: "living_realms",
                table: "FactionResources",
                keyColumn: "Id",
                keyValue: new Guid("51000000-0000-4000-8000-000000000004"));

            migrationBuilder.DeleteData(
                schema: "living_realms",
                table: "FactionResources",
                keyColumn: "Id",
                keyValue: new Guid("51000000-0000-4000-8000-000000000005"));

            migrationBuilder.DeleteData(
                schema: "living_realms",
                table: "FactionStructures",
                keyColumn: "Id",
                keyValue: new Guid("52000000-0000-4000-8000-000000000001"));

            migrationBuilder.DeleteData(
                schema: "living_realms",
                table: "FactionStructures",
                keyColumn: "Id",
                keyValue: new Guid("52000000-0000-4000-8000-000000000002"));

            migrationBuilder.DeleteData(
                schema: "living_realms",
                table: "Settlements",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-4000-8000-000000000001"));

            migrationBuilder.DeleteData(
                schema: "living_realms",
                table: "WorldHistory",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-4000-8000-000000000001"));

            migrationBuilder.DeleteData(
                schema: "living_realms",
                table: "Factions",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-4000-8000-000000000001"));

            migrationBuilder.DropColumn(
                name: "SimulatedHours",
                schema: "living_realms",
                table: "Factions");

        }
    }
}

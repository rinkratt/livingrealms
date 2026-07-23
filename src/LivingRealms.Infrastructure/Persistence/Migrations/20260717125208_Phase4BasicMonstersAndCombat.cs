using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LivingRealms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase4BasicMonstersAndCombat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "AttackRange",
                schema: "living_realms",
                table: "CreatureSpecies",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "DetectionRadius",
                schema: "living_realms",
                table: "CreatureSpecies",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<int>(
                name: "ExperienceReward",
                schema: "living_realms",
                table: "CreatureSpecies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RespawnSeconds",
                schema: "living_realms",
                table: "CreatureSpecies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastAttackAt",
                schema: "living_realms",
                table: "Creatures",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "PositionX",
                schema: "living_realms",
                table: "Creatures",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "PositionY",
                schema: "living_realms",
                table: "Creatures",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "PositionZ",
                schema: "living_realms",
                table: "Creatures",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RespawnAt",
                schema: "living_realms",
                table: "Creatures",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "SpawnX",
                schema: "living_realms",
                table: "Creatures",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "SpawnY",
                schema: "living_realms",
                table: "Creatures",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "SpawnZ",
                schema: "living_realms",
                table: "Creatures",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastAttackAt",
                schema: "living_realms",
                table: "Characters",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.InsertData(
                schema: "living_realms",
                table: "CreatureSpecies",
                columns: new[] { "Id", "AttackRange", "BaseAttack", "BaseDefense", "BaseHealth", "BaseMovementSpeed", "CreatedAt", "DetectionRadius", "ExperienceReward", "IsPersistentByDefault", "Key", "Name", "RespawnSeconds", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("5133411d-cb9d-4f00-a16e-ac106d7cfe91"), 1.8f, 15, 9, 90, 3.6f, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 12f, 90, true, "goblin-raider", "Goblin Raider", 120, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("5ff49fb8-b1db-4a5d-8274-8a0ee8ed4eb2"), 1.7f, 10, 5, 55, 4.2f, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 10f, 45, true, "prairie-wolf", "Prairie Wolf", 75, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("8ac9948d-3b09-4c70-aaf1-0c36f967c5a1"), 1.35f, 4, 2, 30, 3.2f, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 7f, 25, true, "forest-rat", "Forest Rat", 45, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("f3260673-96f8-4d56-ad45-25901cae6f98"), 2.1f, 22, 14, 180, 3.2f, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 15f, 220, true, "goblin-chief", "Goblin Chief", 300, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) }
                });

            migrationBuilder.InsertData(
                schema: "living_realms",
                table: "Creatures",
                columns: new[] { "Id", "Aggression", "Attack", "CreatedAt", "Defense", "Experience", "FactionId", "Health", "LastAttackAt", "LastProcessedAt", "Leadership", "Level", "MaximumHealth", "MovementSpeed", "Name", "PositionX", "PositionY", "PositionZ", "RegionId", "RespawnAt", "Role", "SpawnX", "SpawnY", "SpawnZ", "SpeciesId", "Status", "Title", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("5d8a9637-a327-4f42-8ec3-a292f548d101"), 45, 10, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5, 0L, null, 55, null, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0, 2, 55, 4.2f, "Ashfang", -29f, 0.08f, 12f, new Guid("7139a553-cea3-45e4-9d91-b3a95629b72e"), null, "Wild Creature", -29f, 0.08f, 12f, new Guid("5ff49fb8-b1db-4a5d-8274-8a0ee8ed4eb2"), 0, null, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("5d8a9637-a327-4f42-8ec3-a292f548d102"), 45, 10, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5, 0L, null, 55, null, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0, 2, 55, 4.2f, "Dusthowl", 29f, 0.08f, 16f, new Guid("7139a553-cea3-45e4-9d91-b3a95629b72e"), null, "Wild Creature", 29f, 0.08f, 16f, new Guid("5ff49fb8-b1db-4a5d-8274-8a0ee8ed4eb2"), 0, null, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("8bd3a92f-80a8-46a6-8349-427975490a01"), 20, 4, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, 0L, null, 30, null, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0, 1, 30, 3.2f, "Brambletail", -16f, 0.08f, 17f, new Guid("7139a553-cea3-45e4-9d91-b3a95629b72e"), null, "Wild Creature", -16f, 0.08f, 17f, new Guid("8ac9948d-3b09-4c70-aaf1-0c36f967c5a1"), 0, null, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("8bd3a92f-80a8-46a6-8349-427975490a02"), 20, 4, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, 0L, null, 30, null, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0, 1, 30, 3.2f, "Mosswhisker", -8f, 0.08f, 34f, new Guid("7139a553-cea3-45e4-9d91-b3a95629b72e"), null, "Wild Creature", -8f, 0.08f, 34f, new Guid("8ac9948d-3b09-4c70-aaf1-0c36f967c5a1"), 0, null, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("8bd3a92f-80a8-46a6-8349-427975490a03"), 20, 4, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, 0L, null, 30, null, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0, 1, 30, 3.2f, "Thornsnout", 12f, 0.08f, 32f, new Guid("7139a553-cea3-45e4-9d91-b3a95629b72e"), null, "Wild Creature", 12f, 0.08f, 32f, new Guid("8ac9948d-3b09-4c70-aaf1-0c36f967c5a1"), 0, null, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("9230414d-a60d-46ca-9c59-36cc3b867201"), 70, 15, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 9, 0L, null, 90, null, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0, 5, 90, 3.6f, "Skrit", -23f, 0.08f, -39f, new Guid("7139a553-cea3-45e4-9d91-b3a95629b72e"), null, "Wild Creature", -23f, 0.08f, -39f, new Guid("5133411d-cb9d-4f00-a16e-ac106d7cfe91"), 0, null, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("9230414d-a60d-46ca-9c59-36cc3b867202"), 70, 15, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 9, 0L, null, 90, null, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0, 5, 90, 3.6f, "Vrak", 22f, 0.08f, -39f, new Guid("7139a553-cea3-45e4-9d91-b3a95629b72e"), null, "Wild Creature", 22f, 0.08f, -39f, new Guid("5133411d-cb9d-4f00-a16e-ac106d7cfe91"), 0, null, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("f4c5a7b9-644f-4c85-b18f-ac38294e3001"), 90, 22, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 14, 0L, null, 180, null, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0, 8, 180, 3.2f, "Gorvak", 0f, 0.08f, -41f, new Guid("7139a553-cea3-45e4-9d91-b3a95629b72e"), null, "Chief", 0f, 0.08f, -41f, new Guid("f3260673-96f8-4d56-ad45-25901cae6f98"), 0, "Clan Chief", new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "living_realms",
                table: "Creatures",
                keyColumn: "Id",
                keyValue: new Guid("5d8a9637-a327-4f42-8ec3-a292f548d101"));

            migrationBuilder.DeleteData(
                schema: "living_realms",
                table: "Creatures",
                keyColumn: "Id",
                keyValue: new Guid("5d8a9637-a327-4f42-8ec3-a292f548d102"));

            migrationBuilder.DeleteData(
                schema: "living_realms",
                table: "Creatures",
                keyColumn: "Id",
                keyValue: new Guid("8bd3a92f-80a8-46a6-8349-427975490a01"));

            migrationBuilder.DeleteData(
                schema: "living_realms",
                table: "Creatures",
                keyColumn: "Id",
                keyValue: new Guid("8bd3a92f-80a8-46a6-8349-427975490a02"));

            migrationBuilder.DeleteData(
                schema: "living_realms",
                table: "Creatures",
                keyColumn: "Id",
                keyValue: new Guid("8bd3a92f-80a8-46a6-8349-427975490a03"));

            migrationBuilder.DeleteData(
                schema: "living_realms",
                table: "Creatures",
                keyColumn: "Id",
                keyValue: new Guid("9230414d-a60d-46ca-9c59-36cc3b867201"));

            migrationBuilder.DeleteData(
                schema: "living_realms",
                table: "Creatures",
                keyColumn: "Id",
                keyValue: new Guid("9230414d-a60d-46ca-9c59-36cc3b867202"));

            migrationBuilder.DeleteData(
                schema: "living_realms",
                table: "Creatures",
                keyColumn: "Id",
                keyValue: new Guid("f4c5a7b9-644f-4c85-b18f-ac38294e3001"));

            migrationBuilder.DeleteData(
                schema: "living_realms",
                table: "CreatureSpecies",
                keyColumn: "Id",
                keyValue: new Guid("5133411d-cb9d-4f00-a16e-ac106d7cfe91"));

            migrationBuilder.DeleteData(
                schema: "living_realms",
                table: "CreatureSpecies",
                keyColumn: "Id",
                keyValue: new Guid("5ff49fb8-b1db-4a5d-8274-8a0ee8ed4eb2"));

            migrationBuilder.DeleteData(
                schema: "living_realms",
                table: "CreatureSpecies",
                keyColumn: "Id",
                keyValue: new Guid("8ac9948d-3b09-4c70-aaf1-0c36f967c5a1"));

            migrationBuilder.DeleteData(
                schema: "living_realms",
                table: "CreatureSpecies",
                keyColumn: "Id",
                keyValue: new Guid("f3260673-96f8-4d56-ad45-25901cae6f98"));

            migrationBuilder.DropColumn(
                name: "AttackRange",
                schema: "living_realms",
                table: "CreatureSpecies");

            migrationBuilder.DropColumn(
                name: "DetectionRadius",
                schema: "living_realms",
                table: "CreatureSpecies");

            migrationBuilder.DropColumn(
                name: "ExperienceReward",
                schema: "living_realms",
                table: "CreatureSpecies");

            migrationBuilder.DropColumn(
                name: "RespawnSeconds",
                schema: "living_realms",
                table: "CreatureSpecies");

            migrationBuilder.DropColumn(
                name: "LastAttackAt",
                schema: "living_realms",
                table: "Creatures");

            migrationBuilder.DropColumn(
                name: "PositionX",
                schema: "living_realms",
                table: "Creatures");

            migrationBuilder.DropColumn(
                name: "PositionY",
                schema: "living_realms",
                table: "Creatures");

            migrationBuilder.DropColumn(
                name: "PositionZ",
                schema: "living_realms",
                table: "Creatures");

            migrationBuilder.DropColumn(
                name: "RespawnAt",
                schema: "living_realms",
                table: "Creatures");

            migrationBuilder.DropColumn(
                name: "SpawnX",
                schema: "living_realms",
                table: "Creatures");

            migrationBuilder.DropColumn(
                name: "SpawnY",
                schema: "living_realms",
                table: "Creatures");

            migrationBuilder.DropColumn(
                name: "SpawnZ",
                schema: "living_realms",
                table: "Creatures");

            migrationBuilder.DropColumn(
                name: "LastAttackAt",
                schema: "living_realms",
                table: "Characters");
        }
    }
}

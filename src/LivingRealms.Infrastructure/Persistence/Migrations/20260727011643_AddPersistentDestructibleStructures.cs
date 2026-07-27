using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LivingRealms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPersistentDestructibleStructures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorldStructures",
                schema: "living_realms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Owner = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    ConstructionProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequiredProjectLevel = table.Column<int>(type: "integer", nullable: false),
                    RequiredDevelopmentStage = table.Column<int>(type: "integer", nullable: false),
                    Health = table.Column<int>(type: "integer", nullable: false),
                    MaximumHealth = table.Column<int>(type: "integer", nullable: false),
                    Armor = table.Column<int>(type: "integer", nullable: false),
                    PositionX = table.Column<float>(type: "real", nullable: false),
                    PositionY = table.Column<float>(type: "real", nullable: false),
                    PositionZ = table.Column<float>(type: "real", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    LastDamagedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DestroyedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorldStructures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorldStructures_ConstructionProjects_ConstructionProjectId",
                        column: x => x.ConstructionProjectId,
                        principalSchema: "living_realms",
                        principalTable: "ConstructionProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                schema: "living_realms",
                table: "WorldStructures",
                columns: new[] { "Id", "Armor", "ConstructionProjectId", "CreatedAt", "DestroyedAt", "DisplayOrder", "Health", "Key", "Kind", "LastDamagedAt", "MaximumHealth", "Name", "Owner", "PositionX", "PositionY", "PositionZ", "RequiredDevelopmentStage", "RequiredProjectLevel", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("83000000-0000-4000-8000-000000000001"), 12, null, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 1, 1800, "stonehaven-gate", 1, null, 1800, "Stonehaven Main Gate", 0, 0f, 0.08f, 3.5f, 1, 0, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("83000000-0000-4000-8000-000000000002"), 14, new Guid("81000000-0000-4000-8000-000000000001"), new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 2, 1600, "stonehaven-wall-northwest", 0, null, 1600, "Stonehaven Northwest Wall", 0, -17f, 0.08f, 3.5f, 1, 1, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("83000000-0000-4000-8000-000000000003"), 14, new Guid("81000000-0000-4000-8000-000000000001"), new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 3, 1600, "stonehaven-wall-northeast", 0, null, 1600, "Stonehaven Northeast Wall", 0, 17f, 0.08f, 3.5f, 1, 1, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("83000000-0000-4000-8000-000000000004"), 14, new Guid("81000000-0000-4000-8000-000000000001"), new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 4, 1800, "stonehaven-wall-west", 0, null, 1800, "Stonehaven West Wall", 0, -29f, 0.08f, -16f, 1, 1, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("83000000-0000-4000-8000-000000000005"), 14, new Guid("81000000-0000-4000-8000-000000000001"), new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 5, 1800, "stonehaven-wall-east", 0, null, 1800, "Stonehaven East Wall", 0, 29f, 0.08f, -16f, 1, 1, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("83000000-0000-4000-8000-000000000006"), 14, new Guid("81000000-0000-4000-8000-000000000001"), new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 6, 2200, "stonehaven-wall-south", 0, null, 2200, "Stonehaven South Wall", 0, 0f, 0.08f, -36f, 1, 1, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("83000000-0000-4000-8000-000000000007"), 8, null, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 10, 900, "stonehaven-blacksmith", 2, null, 900, "Stonehaven Blacksmith", 0, -11f, 0.08f, -13f, 1, 0, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("83000000-0000-4000-8000-000000000008"), 7, null, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 11, 850, "stonehaven-inn", 2, null, 850, "Wayfarer Inn", 0, 11f, 0.08f, -14f, 1, 0, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("83000000-0000-4000-8000-000000000009"), 6, null, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 12, 700, "stonehaven-herbalist", 2, null, 700, "Stonehaven Herbalist", 0, -12f, 0.08f, -26f, 1, 0, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("83000000-0000-4000-8000-000000000010"), 9, null, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 13, 1000, "stonehaven-storehouse", 6, null, 1000, "Stonehaven Storehouse", 0, 12f, 0.08f, -27f, 1, 0, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("83000000-0000-4000-8000-000000000011"), 6, new Guid("81000000-0000-4000-8000-000000000003"), new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 14, 950, "stonehaven-lumber-yard", 2, null, 950, "Stonehaven Lumber Yard", 0, -22f, 0.08f, -19.5f, 1, 1, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("83000000-0000-4000-8000-000000000012"), 8, new Guid("81000000-0000-4000-8000-000000000004"), new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 15, 1000, "stonehaven-quarry-works", 2, null, 1000, "Stonehaven Quarry Works", 0, 88f, 0.08f, -91f, 1, 1, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("83000000-0000-4000-8000-000000000013"), 12, null, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 16, 1400, "irondeep-mine", 4, null, 1400, "Irondeep Mine", 0, 108f, 0.08f, -112f, 1, 0, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("83000000-0000-4000-8000-000000000014"), 3, null, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 17, 600, "mirrorwater-dock", 5, null, 600, "Mirrorwater Dock", 0, 82f, 0.08f, -20f, 1, 0, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("83000000-0000-4000-8000-000000000015"), 5, null, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 18, 650, "stonehaven-west-farmhouse", 2, null, 650, "West Farmhouse", 0, -29f, 0.08f, 128f, 1, 0, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("83000000-0000-4000-8000-000000000016"), 5, null, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 19, 650, "stonehaven-east-farmhouse", 2, null, 650, "East Farmhouse", 0, 29f, 0.08f, 128f, 1, 0, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("83000000-0000-4000-8000-000000000017"), 2, null, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 20, 450, "stonehaven-farm-1", 3, null, 450, "Northwest Cropland", 0, -28f, 0.08f, 76f, 1, 0, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("83000000-0000-4000-8000-000000000018"), 2, null, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 21, 450, "stonehaven-farm-2", 3, null, 450, "North Cropland", 0, -9f, 0.08f, 76f, 1, 0, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("83000000-0000-4000-8000-000000000019"), 2, null, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 22, 450, "stonehaven-farm-3", 3, null, 450, "Northeast Cropland", 0, 10f, 0.08f, 76f, 1, 0, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("83000000-0000-4000-8000-000000000020"), 2, null, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 23, 450, "stonehaven-farm-4", 3, null, 450, "East Cropland", 0, 29f, 0.08f, 76f, 1, 0, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("83000000-0000-4000-8000-000000000021"), 2, null, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 24, 450, "stonehaven-farm-5", 3, null, 450, "Southwest Cropland", 0, -28f, 0.08f, 101f, 1, 0, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("83000000-0000-4000-8000-000000000022"), 2, null, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 25, 450, "stonehaven-farm-6", 3, null, 450, "South Cropland", 0, -9f, 0.08f, 101f, 1, 0, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("83000000-0000-4000-8000-000000000023"), 2, null, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 26, 450, "stonehaven-farm-7", 3, null, 450, "Southeast Cropland", 0, 10f, 0.08f, 101f, 1, 0, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("83000000-0000-4000-8000-000000000024"), 2, null, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 27, 450, "stonehaven-farm-8", 3, null, 450, "Far East Cropland", 0, 29f, 0.08f, 101f, 1, 0, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("83000000-0000-4000-8000-000000000025"), 8, new Guid("81000000-0000-4000-8000-000000000002"), new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 40, 1100, "darkwood-palisade-north", 0, null, 1100, "Darkwood North Palisade", 1, -116f, 0.08f, -121f, 1, 1, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("83000000-0000-4000-8000-000000000026"), 8, new Guid("81000000-0000-4000-8000-000000000002"), new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 41, 1100, "darkwood-palisade-east", 0, null, 1100, "Darkwood East Palisade", 1, -99f, 0.08f, -104f, 1, 1, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("83000000-0000-4000-8000-000000000027"), 8, new Guid("81000000-0000-4000-8000-000000000002"), new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 42, 1100, "darkwood-palisade-south", 0, null, 1100, "Darkwood South Palisade", 1, -116f, 0.08f, -87f, 1, 1, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("83000000-0000-4000-8000-000000000028"), 8, new Guid("81000000-0000-4000-8000-000000000002"), new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 43, 1100, "darkwood-palisade-west", 0, null, 1100, "Darkwood West Palisade", 1, -133f, 0.08f, -104f, 1, 1, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("83000000-0000-4000-8000-000000000029"), 4, null, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 44, 800, "darkwood-hide-tents", 2, null, 800, "Darkwood Hide Tents", 1, -119f, 0.08f, -107f, 1, 0, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("83000000-0000-4000-8000-000000000030"), 5, null, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 45, 700, "darkwood-stockpile", 6, null, 700, "Darkwood Crude Stockpile", 1, -124f, 0.08f, -102f, 1, 0, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("83000000-0000-4000-8000-000000000031"), 5, new Guid("81000000-0000-4000-8000-000000000005"), new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 46, 750, "darkwood-supply-hut", 2, null, 750, "Darkwood Supply Hut", 1, -126f, 0.08f, -98f, 1, 1, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("83000000-0000-4000-8000-000000000032"), 6, null, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 47, 900, "darkwood-hunter-lodge", 2, null, 900, "Darkwood Hunter Lodge", 1, -107f, 0.08f, -108f, 2, 0, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("83000000-0000-4000-8000-000000000033"), 8, null, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 48, 1000, "darkwood-watchtower", 2, null, 1000, "Darkwood Watchtower", 1, -126f, 0.08f, -112f, 3, 0, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("83000000-0000-4000-8000-000000000034"), 10, null, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 49, 1150, "darkwood-iron-workshop", 2, null, 1150, "Darkwood Iron Workshop", 1, -107f, 0.08f, -98f, 3, 0, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorldStructures_ConstructionProjectId",
                schema: "living_realms",
                table: "WorldStructures",
                column: "ConstructionProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_WorldStructures_Key",
                schema: "living_realms",
                table: "WorldStructures",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorldStructures_Owner_Kind_DisplayOrder",
                schema: "living_realms",
                table: "WorldStructures",
                columns: new[] { "Owner", "Kind", "DisplayOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorldStructures",
                schema: "living_realms");
        }
    }
}

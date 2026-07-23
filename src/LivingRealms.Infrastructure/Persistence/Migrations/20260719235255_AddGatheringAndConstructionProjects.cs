using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LivingRealms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGatheringAndConstructionProjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastGatherAt",
                schema: "living_realms",
                table: "Characters",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ConstructionProjects",
                schema: "living_realms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Owner = table.Column<int>(type: "integer", nullable: false),
                    SettlementId = table.Column<Guid>(type: "uuid", nullable: true),
                    FactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    PositionX = table.Column<float>(type: "real", nullable: false),
                    PositionY = table.Column<float>(type: "real", nullable: false),
                    PositionZ = table.Column<float>(type: "real", nullable: false),
                    WoodRequired = table.Column<int>(type: "integer", nullable: false),
                    StoneRequired = table.Column<int>(type: "integer", nullable: false),
                    WoodContributed = table.Column<int>(type: "integer", nullable: false),
                    StoneContributed = table.Column<int>(type: "integer", nullable: false),
                    CurrentLevel = table.Column<int>(type: "integer", nullable: false),
                    MaximumLevel = table.Column<int>(type: "integer", nullable: false),
                    LastNpcContributionAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConstructionProjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConstructionProjects_Factions_FactionId",
                        column: x => x.FactionId,
                        principalSchema: "living_realms",
                        principalTable: "Factions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConstructionProjects_Settlements_SettlementId",
                        column: x => x.SettlementId,
                        principalSchema: "living_realms",
                        principalTable: "Settlements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorldResourceNodes",
                schema: "living_realms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RegionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Owner = table.Column<int>(type: "integer", nullable: false),
                    PositionX = table.Column<float>(type: "real", nullable: false),
                    PositionY = table.Column<float>(type: "real", nullable: false),
                    PositionZ = table.Column<float>(type: "real", nullable: false),
                    Remaining = table.Column<int>(type: "integer", nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    YieldPerHarvest = table.Column<int>(type: "integer", nullable: false),
                    RespawnSeconds = table.Column<int>(type: "integer", nullable: false),
                    RespawnAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorldResourceNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorldResourceNodes_Regions_RegionId",
                        column: x => x.RegionId,
                        principalSchema: "living_realms",
                        principalTable: "Regions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ResourceContributions",
                schema: "living_realms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConstructionProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContributorName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceContributions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResourceContributions_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalSchema: "living_realms",
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ResourceContributions_ConstructionProjects_ConstructionProj~",
                        column: x => x.ConstructionProjectId,
                        principalSchema: "living_realms",
                        principalTable: "ConstructionProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "living_realms",
                table: "ConstructionProjects",
                columns: new[] { "Id", "CompletedAt", "CreatedAt", "CurrentLevel", "FactionId", "Key", "LastNpcContributionAt", "MaximumLevel", "Name", "Owner", "PositionX", "PositionY", "PositionZ", "SettlementId", "StoneContributed", "StoneRequired", "UpdatedAt", "WoodContributed", "WoodRequired" },
                values: new object[,]
                {
                    { new Guid("81000000-0000-4000-8000-000000000001"), null, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0, null, "stonehaven-curtain-wall", null, 3, "Stonehaven Curtain Wall", 0, 0f, 0.08f, 5.2f, new Guid("40000000-0000-4000-8000-000000000001"), 0, 300, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0, 240 },
                    { new Guid("81000000-0000-4000-8000-000000000002"), null, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0, new Guid("50000000-0000-4000-8000-000000000001"), "darkwood-perimeter-palisade", null, 3, "Darkwood Perimeter Palisade", 1, -116f, 0.08f, -87f, null, 0, 80, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0, 320 },
                    { new Guid("81000000-0000-4000-8000-000000000003"), null, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0, null, "stonehaven-lumber-yard", null, 3, "Stonehaven Lumber Yard", 0, -31f, 0.08f, -18f, new Guid("40000000-0000-4000-8000-000000000001"), 0, 40, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0, 120 },
                    { new Guid("81000000-0000-4000-8000-000000000004"), null, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0, null, "stonehaven-quarry-works", null, 3, "Stonehaven Quarry Works", 0, 34f, 0.08f, 30f, new Guid("40000000-0000-4000-8000-000000000001"), 0, 150, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0, 70 },
                    { new Guid("81000000-0000-4000-8000-000000000005"), null, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0, new Guid("50000000-0000-4000-8000-000000000001"), "darkwood-supply-hut", null, 3, "Darkwood Supply Hut", 1, -126f, 0.08f, -98f, null, 0, 30, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0, 100 }
                });

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "SettlementResidents",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-4000-8000-000000000008"),
                columns: new[] { "Dialogue", "Role", "WorkX", "WorkZ" },
                values: new object[] { "Every sound timber I bring home becomes a roof, a gate, or one more wall between us and Darkwood.", "Lumberjack", -36.5f, -18f });

            migrationBuilder.InsertData(
                schema: "living_realms",
                table: "SettlementResidents",
                columns: new[] { "Id", "CanFight", "CreatedAt", "Dialogue", "Health", "HomeX", "HomeY", "HomeZ", "MaximumHealth", "Name", "Role", "SafeX", "SafeY", "SafeZ", "SettlementId", "Status", "UpdatedAt", "WorkX", "WorkY", "WorkZ" },
                values: new object[] { new Guid("70000000-0000-4000-8000-000000000009"), false, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Stonehaven's walls begin in the quarry. Give me a strong back and enough daylight.", 95, 7f, 0.08f, -24f, 95, "Dain", "Quarry Worker", 4f, 0.08f, -15f, new Guid("40000000-0000-4000-8000-000000000001"), 0, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 36f, 0.08f, 30f });

            migrationBuilder.InsertData(
                schema: "living_realms",
                table: "WorldResourceNodes",
                columns: new[] { "Id", "Capacity", "CreatedAt", "Key", "Kind", "Name", "Owner", "PositionX", "PositionY", "PositionZ", "RegionId", "Remaining", "RespawnAt", "RespawnSeconds", "UpdatedAt", "YieldPerHarvest" },
                values: new object[,]
                {
                    { new Guid("82000000-0000-4000-8000-000000000001"), 60, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "stonehaven-oak-west", 1, "Westwood Oak", 0, -39f, 0.08f, -18f, new Guid("7139a553-cea3-45e4-9d91-b3a95629b72e"), 60, null, 90, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { new Guid("82000000-0000-4000-8000-000000000002"), 60, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "stonehaven-pine-north", 1, "Northroad Pine", 0, -34f, 0.08f, 28f, new Guid("7139a553-cea3-45e4-9d91-b3a95629b72e"), 60, null, 90, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { new Guid("82000000-0000-4000-8000-000000000003"), 60, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "stonehaven-quarry-east", 2, "East Quarry Face", 0, 39f, 0.08f, 30f, new Guid("7139a553-cea3-45e4-9d91-b3a95629b72e"), 60, null, 110, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { new Guid("82000000-0000-4000-8000-000000000004"), 60, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "stonehaven-boulder-south", 2, "Southroad Stone", 0, 30f, 0.08f, -43f, new Guid("7139a553-cea3-45e4-9d91-b3a95629b72e"), 60, null, 110, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { new Guid("82000000-0000-4000-8000-000000000005"), 70, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "darkwood-pine", 1, "Darkwood Pine", 1, -134f, 0.08f, -91f, new Guid("7139a553-cea3-45e4-9d91-b3a95629b72e"), 70, null, 90, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { new Guid("82000000-0000-4000-8000-000000000006"), 70, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "darkwood-deadfall", 1, "Darkwood Deadfall", 1, -96f, 0.08f, -112f, new Guid("7139a553-cea3-45e4-9d91-b3a95629b72e"), 70, null, 90, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { new Guid("82000000-0000-4000-8000-000000000007"), 70, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "darkwood-stone", 2, "Darkwood Stone Shelf", 1, -132f, 0.08f, -126f, new Guid("7139a553-cea3-45e4-9d91-b3a95629b72e"), 70, null, 110, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConstructionProjects_FactionId",
                schema: "living_realms",
                table: "ConstructionProjects",
                column: "FactionId");

            migrationBuilder.CreateIndex(
                name: "IX_ConstructionProjects_Key",
                schema: "living_realms",
                table: "ConstructionProjects",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConstructionProjects_SettlementId",
                schema: "living_realms",
                table: "ConstructionProjects",
                column: "SettlementId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceContributions_CharacterId",
                schema: "living_realms",
                table: "ResourceContributions",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceContributions_ConstructionProjectId_OccurredAt",
                schema: "living_realms",
                table: "ResourceContributions",
                columns: new[] { "ConstructionProjectId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorldResourceNodes_Key",
                schema: "living_realms",
                table: "WorldResourceNodes",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorldResourceNodes_RegionId_Owner_Kind",
                schema: "living_realms",
                table: "WorldResourceNodes",
                columns: new[] { "RegionId", "Owner", "Kind" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ResourceContributions",
                schema: "living_realms");

            migrationBuilder.DropTable(
                name: "WorldResourceNodes",
                schema: "living_realms");

            migrationBuilder.DropTable(
                name: "ConstructionProjects",
                schema: "living_realms");

            migrationBuilder.DeleteData(
                schema: "living_realms",
                table: "SettlementResidents",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-4000-8000-000000000009"));

            migrationBuilder.DropColumn(
                name: "LastGatherAt",
                schema: "living_realms",
                table: "Characters");

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "SettlementResidents",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-4000-8000-000000000008"),
                columns: new[] { "Dialogue", "Role", "WorkX", "WorkZ" },
                values: new object[] { "Stonehaven is small, but it is ours. That is reason enough to defend it.", "Villager", -5f, -17f });
        }
    }
}

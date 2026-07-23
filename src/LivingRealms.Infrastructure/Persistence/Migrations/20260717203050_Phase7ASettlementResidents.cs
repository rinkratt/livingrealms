using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LivingRealms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase7ASettlementResidents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SettlementResidents",
                schema: "living_realms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SettlementId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Role = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Health = table.Column<int>(type: "integer", nullable: false),
                    MaximumHealth = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CanFight = table.Column<bool>(type: "boolean", nullable: false),
                    HomeX = table.Column<float>(type: "real", nullable: false),
                    HomeY = table.Column<float>(type: "real", nullable: false),
                    HomeZ = table.Column<float>(type: "real", nullable: false),
                    WorkX = table.Column<float>(type: "real", nullable: false),
                    WorkY = table.Column<float>(type: "real", nullable: false),
                    WorkZ = table.Column<float>(type: "real", nullable: false),
                    SafeX = table.Column<float>(type: "real", nullable: false),
                    SafeY = table.Column<float>(type: "real", nullable: false),
                    SafeZ = table.Column<float>(type: "real", nullable: false),
                    Dialogue = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SettlementResidents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SettlementResidents_Settlements_SettlementId",
                        column: x => x.SettlementId,
                        principalSchema: "living_realms",
                        principalTable: "Settlements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "living_realms",
                table: "SettlementResidents",
                columns: new[] { "Id", "CanFight", "CreatedAt", "Dialogue", "Health", "HomeX", "HomeY", "HomeZ", "MaximumHealth", "Name", "Role", "SafeX", "SafeY", "SafeZ", "SettlementId", "Status", "UpdatedAt", "WorkX", "WorkY", "WorkZ" },
                values: new object[,]
                {
                    { new Guid("70000000-0000-4000-8000-000000000001"), true, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Keep your eyes on the northern road. Darkwood has been bolder every night.", 145, -4f, 0.08f, -19f, 145, "Captain Rowan", "Guard Captain", 0f, 0.08f, -11f, new Guid("40000000-0000-4000-8000-000000000001"), 0, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0f, 0.08f, 0.5f },
                    { new Guid("70000000-0000-4000-8000-000000000002"), true, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "The gate is quiet for now. I would prefer it stayed that way.", 115, -7f, 0.08f, -21f, 115, "Mira", "Stonehaven Guard", -3f, 0.08f, -11f, new Guid("40000000-0000-4000-8000-000000000001"), 0, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), -2.5f, 0.08f, 1.5f },
                    { new Guid("70000000-0000-4000-8000-000000000003"), true, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "If the horn sounds, get behind the palisade and let us hold the gate.", 115, 7f, 0.08f, -21f, 115, "Tomas", "Stonehaven Guard", 3f, 0.08f, -11f, new Guid("40000000-0000-4000-8000-000000000001"), 0, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2.5f, 0.08f, 1.5f },
                    { new Guid("70000000-0000-4000-8000-000000000004"), true, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Good iron remembers the hand that shaped it. Bring me ore and I will show you.", 105, -15f, 0.08f, -17f, 105, "Brann", "Blacksmith", -8f, 0.08f, -14f, new Guid("40000000-0000-4000-8000-000000000001"), 0, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), -11f, 0.08f, -9.2f },
                    { new Guid("70000000-0000-4000-8000-000000000005"), false, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "The hearth is warm, the stew is honest, and the rumors are free.", 90, 15f, 0.08f, -18f, 90, "Mara", "Innkeeper", 8f, 0.08f, -14f, new Guid("40000000-0000-4000-8000-000000000001"), 0, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 11f, 0.08f, -10.2f },
                    { new Guid("70000000-0000-4000-8000-000000000006"), false, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Wounds heal faster when they are tended before pride makes them worse.", 85, -16f, 0.08f, -29f, 85, "Elowen", "Healer", -8f, 0.08f, -18f, new Guid("40000000-0000-4000-8000-000000000001"), 0, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), -12f, 0.08f, -22.6f },
                    { new Guid("70000000-0000-4000-8000-000000000007"), false, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Supplies are counted twice these days. Trouble makes every loaf and arrow matter.", 95, 16f, 0.08f, -30f, 95, "Oren", "Storekeeper", 8f, 0.08f, -18f, new Guid("40000000-0000-4000-8000-000000000001"), 0, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 12f, 0.08f, -23.6f },
                    { new Guid("70000000-0000-4000-8000-000000000008"), false, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Stonehaven is small, but it is ours. That is reason enough to defend it.", 80, -7f, 0.08f, -23f, 80, "Nessa", "Villager", -4f, 0.08f, -15f, new Guid("40000000-0000-4000-8000-000000000001"), 0, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), -5f, 0.08f, -17f }
                });

            migrationBuilder.CreateIndex(
                name: "IX_SettlementResidents_SettlementId_Name",
                schema: "living_realms",
                table: "SettlementResidents",
                columns: new[] { "SettlementId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SettlementResidents",
                schema: "living_realms");
        }
    }
}

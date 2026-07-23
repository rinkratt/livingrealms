using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LivingRealms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase5LootEquipmentAndSkills : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CharacterInventory_Items_ItemId",
                schema: "living_realms",
                table: "CharacterInventory");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                schema: "living_realms",
                table: "Items",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AttackBonus",
                schema: "living_realms",
                table: "Items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DefenseBonus",
                schema: "living_realms",
                table: "Items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EquipmentSlot",
                schema: "living_realms",
                table: "Items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HealingAmount",
                schema: "living_realms",
                table: "Items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Rarity",
                schema: "living_realms",
                table: "Items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RequiredArchetype",
                schema: "living_realms",
                table: "Items",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CharacterSkills",
                schema: "living_realms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Experience = table.Column<long>(type: "bigint", nullable: false),
                    LastUsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterSkills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterSkills_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalSchema: "living_realms",
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "living_realms",
                table: "Items",
                columns: new[] { "Id", "AttackBonus", "BaseValue", "CreatedAt", "DefenseBonus", "Description", "EquipmentSlot", "HealingAmount", "Key", "Kind", "Name", "Rarity", "RequiredArchetype", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("105a7b69-0e17-40d0-8d0f-4aa63bfb1001"), 5, 35, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0, "A balanced iron blade issued to new vanguards.", 0, 0, "stonehaven-training-blade", 1, "Stonehaven Training Blade", 0, 0, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("105a7b69-0e17-40d0-8d0f-4aa63bfb1002"), 5, 35, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0, "A reliable yew bow issued to new rangers.", 0, 0, "stonehaven-hunting-bow", 1, "Stonehaven Hunting Bow", 0, 1, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("105a7b69-0e17-40d0-8d0f-4aa63bfb1003"), 0, 30, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 3, "Layered leather that softens claws and rough blades.", 1, 0, "stonehaven-leather-guard", 2, "Stonehaven Leather Guard", 0, null, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("105a7b69-0e17-40d0-8d0f-4aa63bfb1004"), 0, 20, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0, "A sharp herbal draught that restores 35 health.", null, 35, "field-tonic", 3, "Field Tonic", 1, null, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("105a7b69-0e17-40d0-8d0f-4aa63bfb1005"), 0, 3, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0, "Proof that a Stonehaven field rat was defeated.", null, 0, "forest-rat-tail", 4, "Forest Rat Tail", 0, null, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("105a7b69-0e17-40d0-8d0f-4aa63bfb1006"), 0, 45, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5, "A thick pelt that can be equipped as light armor.", 1, 0, "prairie-wolf-pelt", 2, "Prairie Wolf Pelt", 1, null, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("105a7b69-0e17-40d0-8d0f-4aa63bfb1007"), 9, 80, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0, "A brutal but effective weapon recovered from a raider.", 0, 0, "goblin-raider-blade", 1, "Goblin Raider Blade", 1, 0, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("105a7b69-0e17-40d0-8d0f-4aa63bfb1008"), 8, 80, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0, "A horn-backed bow adapted for a Stonehaven ranger.", 0, 0, "goblin-raider-bow", 1, "Goblin Raider Bow", 1, 1, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("105a7b69-0e17-40d0-8d0f-4aa63bfb1009"), 14, 180, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0, "The heavy notched blade carried by the goblin chief.", 0, 0, "gorvaks-warblade", 1, "Gorvak's Warblade", 2, 0, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("105a7b69-0e17-40d0-8d0f-4aa63bfb1010"), 13, 180, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0, "A captured warbow restrung for Elara's reach.", 0, 0, "gorvaks-warbow", 1, "Gorvak's Warbow", 2, 1, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_CharacterSkills_CharacterId_SkillKey",
                schema: "living_realms",
                table: "CharacterSkills",
                columns: new[] { "CharacterId", "SkillKey" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CharacterInventory_Items_ItemId",
                schema: "living_realms",
                table: "CharacterInventory",
                column: "ItemId",
                principalSchema: "living_realms",
                principalTable: "Items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CharacterInventory_Items_ItemId",
                schema: "living_realms",
                table: "CharacterInventory");

            migrationBuilder.DropTable(
                name: "CharacterSkills",
                schema: "living_realms");

            migrationBuilder.DeleteData(
                schema: "living_realms",
                table: "Items",
                keyColumn: "Id",
                keyValue: new Guid("105a7b69-0e17-40d0-8d0f-4aa63bfb1001"));

            migrationBuilder.DeleteData(
                schema: "living_realms",
                table: "Items",
                keyColumn: "Id",
                keyValue: new Guid("105a7b69-0e17-40d0-8d0f-4aa63bfb1002"));

            migrationBuilder.DeleteData(
                schema: "living_realms",
                table: "Items",
                keyColumn: "Id",
                keyValue: new Guid("105a7b69-0e17-40d0-8d0f-4aa63bfb1003"));

            migrationBuilder.DeleteData(
                schema: "living_realms",
                table: "Items",
                keyColumn: "Id",
                keyValue: new Guid("105a7b69-0e17-40d0-8d0f-4aa63bfb1004"));

            migrationBuilder.DeleteData(
                schema: "living_realms",
                table: "Items",
                keyColumn: "Id",
                keyValue: new Guid("105a7b69-0e17-40d0-8d0f-4aa63bfb1005"));

            migrationBuilder.DeleteData(
                schema: "living_realms",
                table: "Items",
                keyColumn: "Id",
                keyValue: new Guid("105a7b69-0e17-40d0-8d0f-4aa63bfb1006"));

            migrationBuilder.DeleteData(
                schema: "living_realms",
                table: "Items",
                keyColumn: "Id",
                keyValue: new Guid("105a7b69-0e17-40d0-8d0f-4aa63bfb1007"));

            migrationBuilder.DeleteData(
                schema: "living_realms",
                table: "Items",
                keyColumn: "Id",
                keyValue: new Guid("105a7b69-0e17-40d0-8d0f-4aa63bfb1008"));

            migrationBuilder.DeleteData(
                schema: "living_realms",
                table: "Items",
                keyColumn: "Id",
                keyValue: new Guid("105a7b69-0e17-40d0-8d0f-4aa63bfb1009"));

            migrationBuilder.DeleteData(
                schema: "living_realms",
                table: "Items",
                keyColumn: "Id",
                keyValue: new Guid("105a7b69-0e17-40d0-8d0f-4aa63bfb1010"));

            migrationBuilder.DropColumn(
                name: "AttackBonus",
                schema: "living_realms",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "DefenseBonus",
                schema: "living_realms",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "EquipmentSlot",
                schema: "living_realms",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "HealingAmount",
                schema: "living_realms",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "Rarity",
                schema: "living_realms",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "RequiredArchetype",
                schema: "living_realms",
                table: "Items");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                schema: "living_realms",
                table: "Items",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CharacterInventory_Items_ItemId",
                schema: "living_realms",
                table: "CharacterInventory",
                column: "ItemId",
                principalSchema: "living_realms",
                principalTable: "Items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

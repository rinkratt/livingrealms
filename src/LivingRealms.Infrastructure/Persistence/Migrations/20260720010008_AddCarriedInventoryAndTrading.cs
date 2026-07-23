using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LivingRealms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCarriedInventoryAndTrading : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UnitWeight",
                schema: "living_realms",
                table: "Items",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "CarryCapacity",
                schema: "living_realms",
                table: "Characters",
                type: "integer",
                nullable: false,
                defaultValue: 80);

            migrationBuilder.AddColumn<int>(
                name: "Gold",
                schema: "living_realms",
                table: "Characters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastContributionAt",
                schema: "living_realms",
                table: "Characters",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "Items",
                keyColumn: "Id",
                keyValue: new Guid("105a7b69-0e17-40d0-8d0f-4aa63bfb1001"),
                column: "UnitWeight",
                value: 6);

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "Items",
                keyColumn: "Id",
                keyValue: new Guid("105a7b69-0e17-40d0-8d0f-4aa63bfb1002"),
                column: "UnitWeight",
                value: 5);

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "Items",
                keyColumn: "Id",
                keyValue: new Guid("105a7b69-0e17-40d0-8d0f-4aa63bfb1003"),
                column: "UnitWeight",
                value: 8);

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "Items",
                keyColumn: "Id",
                keyValue: new Guid("105a7b69-0e17-40d0-8d0f-4aa63bfb1004"),
                column: "UnitWeight",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "Items",
                keyColumn: "Id",
                keyValue: new Guid("105a7b69-0e17-40d0-8d0f-4aa63bfb1005"),
                columns: new[] { "Description", "UnitWeight" },
                values: new object[] { "Oren buys these as proof that the grain stores are being protected.", 1 });

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "Items",
                keyColumn: "Id",
                keyValue: new Guid("105a7b69-0e17-40d0-8d0f-4aa63bfb1006"),
                column: "UnitWeight",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "Items",
                keyColumn: "Id",
                keyValue: new Guid("105a7b69-0e17-40d0-8d0f-4aa63bfb1007"),
                column: "UnitWeight",
                value: 7);

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "Items",
                keyColumn: "Id",
                keyValue: new Guid("105a7b69-0e17-40d0-8d0f-4aa63bfb1008"),
                column: "UnitWeight",
                value: 6);

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "Items",
                keyColumn: "Id",
                keyValue: new Guid("105a7b69-0e17-40d0-8d0f-4aa63bfb1009"),
                column: "UnitWeight",
                value: 9);

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "Items",
                keyColumn: "Id",
                keyValue: new Guid("105a7b69-0e17-40d0-8d0f-4aa63bfb1010"),
                column: "UnitWeight",
                value: 8);

            migrationBuilder.InsertData(
                schema: "living_realms",
                table: "Items",
                columns: new[] { "Id", "AttackBonus", "BaseValue", "CreatedAt", "DefenseBonus", "Description", "EquipmentSlot", "HealingAmount", "Key", "Kind", "Name", "Rarity", "RequiredArchetype", "UnitWeight", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("105a7b69-0e17-40d0-8d0f-4aa63bfb1011"), 0, 2, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0, "Sound timber used by Stonehaven's builders. Construction projects and Oren both need it.", null, 0, "raw-timber", 4, "Raw Timber", 0, null, 1, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("105a7b69-0e17-40d0-8d0f-4aa63bfb1012"), 0, 3, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0, "Quarried stone used in walls and foundations. Construction projects and Oren both need it.", null, 0, "rough-stone", 4, "Rough Stone", 0, null, 2, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "living_realms",
                table: "Items",
                keyColumn: "Id",
                keyValue: new Guid("105a7b69-0e17-40d0-8d0f-4aa63bfb1011"));

            migrationBuilder.DeleteData(
                schema: "living_realms",
                table: "Items",
                keyColumn: "Id",
                keyValue: new Guid("105a7b69-0e17-40d0-8d0f-4aa63bfb1012"));

            migrationBuilder.DropColumn(
                name: "UnitWeight",
                schema: "living_realms",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "CarryCapacity",
                schema: "living_realms",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "Gold",
                schema: "living_realms",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "LastContributionAt",
                schema: "living_realms",
                table: "Characters");

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "Items",
                keyColumn: "Id",
                keyValue: new Guid("105a7b69-0e17-40d0-8d0f-4aa63bfb1005"),
                column: "Description",
                value: "Proof that a Stonehaven field rat was defeated.");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LivingRealms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase2AccountsAndCharacters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpiresAt",
                schema: "living_realms",
                table: "PlayerSessions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSeenAt",
                schema: "living_realms",
                table: "PlayerSessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TokenHash",
                schema: "living_realms",
                table: "PlayerSessions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserAgent",
                schema: "living_realms",
                table: "PlayerSessions",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Archetype",
                schema: "living_realms",
                table: "Characters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.InsertData(
                schema: "living_realms",
                table: "Regions",
                columns: new[] { "Id", "CreatedAt", "Description", "Key", "Name", "ThreatLevel", "UpdatedAt" },
                values: new object[] { new Guid("7139a553-cea3-45e4-9d91-b3a95629b72e"), new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "The first playable valley of Living Realms.", "stonehaven-valley", "Stonehaven Valley", 1, new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerSessions_TokenHash",
                schema: "living_realms",
                table: "PlayerSessions",
                column: "TokenHash",
                unique: true,
                filter: "\"TokenHash\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlayerSessions_TokenHash",
                schema: "living_realms",
                table: "PlayerSessions");

            migrationBuilder.DeleteData(
                schema: "living_realms",
                table: "Regions",
                keyColumn: "Id",
                keyValue: new Guid("7139a553-cea3-45e4-9d91-b3a95629b72e"));

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                schema: "living_realms",
                table: "PlayerSessions");

            migrationBuilder.DropColumn(
                name: "LastSeenAt",
                schema: "living_realms",
                table: "PlayerSessions");

            migrationBuilder.DropColumn(
                name: "TokenHash",
                schema: "living_realms",
                table: "PlayerSessions");

            migrationBuilder.DropColumn(
                name: "UserAgent",
                schema: "living_realms",
                table: "PlayerSessions");

            migrationBuilder.DropColumn(
                name: "Archetype",
                schema: "living_realms",
                table: "Characters");
        }
    }
}

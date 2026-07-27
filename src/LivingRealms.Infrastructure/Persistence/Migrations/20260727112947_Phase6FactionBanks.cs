using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LivingRealms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase6FactionBanks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FactionBanks",
                schema: "living_realms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Owner = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    GoldBalance = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FactionBanks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FactionBankInventory",
                schema: "living_realms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BankId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    BankBuyPrice = table.Column<int>(type: "integer", nullable: false),
                    BankSellPrice = table.Column<int>(type: "integer", nullable: false),
                    LastPurchasedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastSoldAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FactionBankInventory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FactionBankInventory_FactionBanks_BankId",
                        column: x => x.BankId,
                        principalSchema: "living_realms",
                        principalTable: "FactionBanks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FactionBankTransactions",
                schema: "living_realms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BankId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<int>(type: "integer", nullable: false),
                    TotalGold = table.Column<int>(type: "integer", nullable: false),
                    BankGoldAfter = table.Column<int>(type: "integer", nullable: false),
                    FactionGoldAfter = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FactionBankTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FactionBankTransactions_FactionBanks_BankId",
                        column: x => x.BankId,
                        principalSchema: "living_realms",
                        principalTable: "FactionBanks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "living_realms",
                table: "FactionBanks",
                columns: new[] { "Id", "CreatedAt", "GoldBalance", "Name", "Owner", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("85000000-0000-4000-8000-000000000001"), new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 300, "Stonehaven Exchange", 0, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("85000000-0000-4000-8000-000000000002"), new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 300, "Darkwood Clan Vault", 1, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) }
                });

            migrationBuilder.InsertData(
                schema: "living_realms",
                table: "FactionBankInventory",
                columns: new[] { "Id", "BankBuyPrice", "BankId", "BankSellPrice", "CreatedAt", "Kind", "LastPurchasedAt", "LastSoldAt", "Quantity", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("85100000-0000-4000-8000-000000000001"), 1, new Guid("85000000-0000-4000-8000-000000000001"), 2, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0, null, null, 0, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("85100000-0000-4000-8000-000000000002"), 2, new Guid("85000000-0000-4000-8000-000000000001"), 3, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1, null, null, 0, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("85100000-0000-4000-8000-000000000003"), 3, new Guid("85000000-0000-4000-8000-000000000001"), 5, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, null, null, 0, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("85100000-0000-4000-8000-000000000004"), 6, new Guid("85000000-0000-4000-8000-000000000001"), 9, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 3, null, null, 0, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("85100000-0000-4000-8000-000000000005"), 1, new Guid("85000000-0000-4000-8000-000000000002"), 2, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0, null, null, 0, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("85100000-0000-4000-8000-000000000006"), 2, new Guid("85000000-0000-4000-8000-000000000002"), 3, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1, null, null, 0, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("85100000-0000-4000-8000-000000000007"), 3, new Guid("85000000-0000-4000-8000-000000000002"), 5, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, null, null, 0, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("85100000-0000-4000-8000-000000000008"), 6, new Guid("85000000-0000-4000-8000-000000000002"), 9, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 3, null, null, 0, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_FactionBankInventory_BankId_Kind",
                schema: "living_realms",
                table: "FactionBankInventory",
                columns: new[] { "BankId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FactionBanks_Owner",
                schema: "living_realms",
                table: "FactionBanks",
                column: "Owner",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FactionBankTransactions_BankId_OccurredAt",
                schema: "living_realms",
                table: "FactionBankTransactions",
                columns: new[] { "BankId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FactionBankInventory",
                schema: "living_realms");

            migrationBuilder.DropTable(
                name: "FactionBankTransactions",
                schema: "living_realms");

            migrationBuilder.DropTable(
                name: "FactionBanks",
                schema: "living_realms");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LivingRealms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase7DestructionRecovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SettlementRecoveries",
                schema: "living_realms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Owner = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FoundingPopulation = table.Column<int>(type: "integer", nullable: false),
                    DefeatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RecoveryEligibleAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RebuildingStartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastProgressedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RecoveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CurrentStructureKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RebuildCycles = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SettlementRecoveries", x => x.Id);
                });

            migrationBuilder.InsertData(
                schema: "living_realms",
                table: "SettlementRecoveries",
                columns: new[] { "Id", "CreatedAt", "CurrentStructureKey", "DefeatedAt", "FoundingPopulation", "LastProgressedAt", "Owner", "RebuildCycles", "RebuildingStartedAt", "RecoveredAt", "RecoveryEligibleAt", "Status", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("86000000-0000-4000-8000-000000000001"), new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, 11, null, 0, 0, null, null, null, 0, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("86000000-0000-4000-8000-000000000002"), new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, 7, null, 1, 0, null, null, null, 0, new DateTimeOffset(new DateTime(2026, 7, 17, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_SettlementRecoveries_Owner",
                schema: "living_realms",
                table: "SettlementRecoveries",
                column: "Owner",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SettlementRecoveries",
                schema: "living_realms");
        }
    }
}

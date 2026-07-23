using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LivingRealms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStonehavenCounterattacks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StonehavenAssaults",
                schema: "living_realms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SettlementId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefendingFactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    WorldDay = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastAdvancedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    InitialSoldierCount = table.Column<int>(type: "integer", nullable: false),
                    SoldiersRemaining = table.Column<int>(type: "integer", nullable: false),
                    InitialGoblinCount = table.Column<int>(type: "integer", nullable: false),
                    GoblinsRemaining = table.Column<int>(type: "integer", nullable: false),
                    CampLevelBefore = table.Column<int>(type: "integer", nullable: false),
                    CampLevelAfter = table.Column<int>(type: "integer", nullable: false),
                    InitialCampStrength = table.Column<int>(type: "integer", nullable: false),
                    CampStrength = table.Column<int>(type: "integer", nullable: false),
                    StonehavenCasualties = table.Column<int>(type: "integer", nullable: false),
                    DarkwoodCasualties = table.Column<int>(type: "integer", nullable: false),
                    OutcomeSummary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StonehavenAssaults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StonehavenAssaults_Factions_DefendingFactionId",
                        column: x => x.DefendingFactionId,
                        principalSchema: "living_realms",
                        principalTable: "Factions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StonehavenAssaults_Settlements_SettlementId",
                        column: x => x.SettlementId,
                        principalSchema: "living_realms",
                        principalTable: "Settlements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StonehavenAssaultMembers",
                schema: "living_realms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssaultId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResidentId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDefeated = table.Column<bool>(type: "boolean", nullable: false),
                    DefeatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StonehavenAssaultMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StonehavenAssaultMembers_SettlementResidents_ResidentId",
                        column: x => x.ResidentId,
                        principalSchema: "living_realms",
                        principalTable: "SettlementResidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StonehavenAssaultMembers_StonehavenAssaults_AssaultId",
                        column: x => x.AssaultId,
                        principalSchema: "living_realms",
                        principalTable: "StonehavenAssaults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StonehavenAssaultMembers_AssaultId_IsDefeated",
                schema: "living_realms",
                table: "StonehavenAssaultMembers",
                columns: new[] { "AssaultId", "IsDefeated" });

            migrationBuilder.CreateIndex(
                name: "IX_StonehavenAssaultMembers_AssaultId_ResidentId",
                schema: "living_realms",
                table: "StonehavenAssaultMembers",
                columns: new[] { "AssaultId", "ResidentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StonehavenAssaultMembers_ResidentId",
                schema: "living_realms",
                table: "StonehavenAssaultMembers",
                column: "ResidentId");

            migrationBuilder.CreateIndex(
                name: "IX_StonehavenAssaults_DefendingFactionId_Status_StartedAt",
                schema: "living_realms",
                table: "StonehavenAssaults",
                columns: new[] { "DefendingFactionId", "Status", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StonehavenAssaults_SettlementId",
                schema: "living_realms",
                table: "StonehavenAssaults",
                column: "SettlementId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StonehavenAssaultMembers",
                schema: "living_realms");

            migrationBuilder.DropTable(
                name: "StonehavenAssaults",
                schema: "living_realms");

        }
    }
}

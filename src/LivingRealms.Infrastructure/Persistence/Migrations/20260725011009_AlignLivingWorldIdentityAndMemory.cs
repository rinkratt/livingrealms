using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LivingRealms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AlignLivingWorldIdentityAndMemory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "Experience",
                schema: "living_realms",
                table: "SettlementResidents",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<bool>(
                name: "IsMajor",
                schema: "living_realms",
                table: "SettlementResidents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MemorySummary",
                schema: "living_realms",
                table: "SettlementResidents",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PrimarySkill",
                schema: "living_realms",
                table: "SettlementResidents",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SkillLevel",
                schema: "living_realms",
                table: "SettlementResidents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Trait",
                schema: "living_realms",
                table: "SettlementResidents",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "SettlementResidents",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-4000-8000-000000000001"),
                columns: new[] { "CanFight", "Dialogue", "Experience", "IsMajor", "MaximumHealth", "MemorySummary", "Name", "PrimarySkill", "Role", "SkillLevel", "Trait", "WorkZ" },
                values: new object[] { false, "Stonehaven survives by remembering every promise, shortage, and warning before it becomes a crisis.", 420L, true, 115, "The village council appointed Aldric to coordinate Stonehaven's stores, defenses, and growing households.", "Reeve Aldric Vale", "Administration", "Reeve of Stonehaven", 5, "Steadfast", -14f });

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "SettlementResidents",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-4000-8000-000000000002"),
                columns: new[] { "Dialogue", "Experience", "IsMajor", "MaximumHealth", "MemorySummary", "PrimarySkill", "Role", "SkillLevel", "Trait" },
                values: new object[] { "The gate is quiet for now. My patrols intend to keep it that way.", 360L, true, 135, "Mira earned command of Stonehaven's guard after organizing the defense of the eastern farms.", "Command", "Guard Captain", 4, "Disciplined" });

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "SettlementResidents",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-4000-8000-000000000003"),
                columns: new[] { "Experience", "IsMajor", "MemorySummary", "PrimarySkill", "SkillLevel", "Trait" },
                values: new object[] { 180L, false, "Tomas has served the northern watch through three Darkwood alarms.", "Patrol", 3, "Loyal" });

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "SettlementResidents",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-4000-8000-000000000004"),
                columns: new[] { "Experience", "IsMajor", "MemorySummary", "PrimarySkill", "SkillLevel", "Trait" },
                values: new object[] { 320L, true, "Brann repaired the guard's weapons during Stonehaven's first recorded Darkwood raid.", "Blacksmithing", 4, "Exacting" });

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "SettlementResidents",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-4000-8000-000000000005"),
                columns: new[] { "CanFight", "Dialogue", "Experience", "IsMajor", "MaximumHealth", "MemorySummary", "Name", "PrimarySkill", "Role", "SkillLevel", "Trait", "WorkX", "WorkZ" },
                values: new object[] { true, "I joined the militia because Stonehaven needed another shield, not because anyone promised I would become a hero.", 80L, false, 95, "Mara Venn was last seen scouting beyond the northern road; her fate remains unresolved.", "Mara Venn", "Swordsmanship", "Militia Recruit", 2, "Courageous", 7f, -2f });

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "SettlementResidents",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-4000-8000-000000000006"),
                columns: new[] { "Experience", "IsMajor", "MemorySummary", "PrimarySkill", "SkillLevel", "Trait" },
                values: new object[] { 300L, true, "Elowen kept Stonehaven's wounded alive through the first night of the gate raid.", "Medicine", 4, "Compassionate" });

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "SettlementResidents",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-4000-8000-000000000007"),
                columns: new[] { "Experience", "IsMajor", "MemorySummary", "PrimarySkill", "SkillLevel", "Trait" },
                values: new object[] { 210L, false, "Oren began recording reserve thresholds after shortages nearly stopped the wall works.", "Trade", 3, "Prudent" });

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "SettlementResidents",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-4000-8000-000000000008"),
                columns: new[] { "Experience", "IsMajor", "MemorySummary", "PrimarySkill", "SkillLevel", "Trait" },
                values: new object[] { 190L, false, "Nessa took responsibility for the timber assigned to Stonehaven's curtain wall.", "Woodcutting", 3, "Resolute" });

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "SettlementResidents",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-4000-8000-000000000009"),
                columns: new[] { "Experience", "IsMajor", "MemorySummary", "PrimarySkill", "SkillLevel", "Trait" },
                values: new object[] { 190L, false, "Dain marks every stone load so the wall ledger can explain where its strength came from.", "Quarrying", 3, "Patient" });

            migrationBuilder.Sql(
                """
                UPDATE living_realms."SettlementResidents"
                SET
                    "PrimarySkill" = CASE "Role"
                        WHEN 'Miner' THEN 'Mining'
                        WHEN 'Fisher' THEN 'Fishing'
                        WHEN 'Farmer' THEN 'Farming'
                        WHEN 'Stonehaven Guard' THEN 'Patrol'
                        WHEN 'Carpenter' THEN 'Carpentry'
                        WHEN 'Mason' THEN 'Masonry'
                        WHEN 'Hunter' THEN 'Tracking'
                        WHEN 'Weaver' THEN 'Weaving'
                        WHEN 'Baker' THEN 'Baking'
                        WHEN 'Tanner' THEN 'Tanning'
                        WHEN 'Brewer' THEN 'Brewing'
                        WHEN 'Stablehand' THEN 'Animal Handling'
                        WHEN 'Herbalist' THEN 'Herbalism'
                        WHEN 'Scribe' THEN 'Recordkeeping'
                        WHEN 'Potter' THEN 'Pottery'
                        ELSE 'Local Knowledge'
                    END,
                    "SkillLevel" = CASE WHEN "SkillLevel" < 1 THEN 1 ELSE "SkillLevel" END,
                    "Trait" = CASE WHEN "Trait" = '' THEN 'Determined' ELSE "Trait" END,
                    "MemorySummary" = CASE WHEN "MemorySummary" = ''
                        THEN "Name" || ' joined Stonehaven as a ' || lower("Role") || ' and now belongs to its persistent history.'
                        ELSE "MemorySummary"
                    END
                WHERE "PrimarySkill" = '' OR "SkillLevel" < 1 OR "Trait" = '' OR "MemorySummary" = '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Experience",
                schema: "living_realms",
                table: "SettlementResidents");

            migrationBuilder.DropColumn(
                name: "IsMajor",
                schema: "living_realms",
                table: "SettlementResidents");

            migrationBuilder.DropColumn(
                name: "MemorySummary",
                schema: "living_realms",
                table: "SettlementResidents");

            migrationBuilder.DropColumn(
                name: "PrimarySkill",
                schema: "living_realms",
                table: "SettlementResidents");

            migrationBuilder.DropColumn(
                name: "SkillLevel",
                schema: "living_realms",
                table: "SettlementResidents");

            migrationBuilder.DropColumn(
                name: "Trait",
                schema: "living_realms",
                table: "SettlementResidents");

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "SettlementResidents",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-4000-8000-000000000001"),
                columns: new[] { "CanFight", "Dialogue", "Health", "MaximumHealth", "Name", "Role", "WorkZ" },
                values: new object[] { true, "Keep your eyes on the northern road. Darkwood has been bolder every night.", 145, 145, "Captain Rowan", "Guard Captain", 0.5f });

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "SettlementResidents",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-4000-8000-000000000002"),
                columns: new[] { "Dialogue", "Health", "MaximumHealth", "Role" },
                values: new object[] { "The gate is quiet for now. I would prefer it stayed that way.", 115, 115, "Stonehaven Guard" });

            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "SettlementResidents",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-4000-8000-000000000005"),
                columns: new[] { "CanFight", "Dialogue", "Health", "MaximumHealth", "Name", "Role", "WorkX", "WorkZ" },
                values: new object[] { false, "The hearth is warm, the stew is honest, and the rumors are free.", 90, 90, "Mara", "Innkeeper", 11f, -10.2f });
        }
    }
}

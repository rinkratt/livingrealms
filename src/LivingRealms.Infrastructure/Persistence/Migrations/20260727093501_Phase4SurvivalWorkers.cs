using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LivingRealms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase4SurvivalWorkers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE living_realms."Settlements"
                SET "Population" = GREATEST("Population", 11),
                    "UpdatedAt" = CURRENT_TIMESTAMP
                WHERE "Id" = '40000000-0000-4000-8000-000000000001';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE living_realms."Settlements"
                SET "Population" = 8,
                    "UpdatedAt" = CURRENT_TIMESTAMP
                WHERE "Id" = '40000000-0000-4000-8000-000000000001'
                  AND "Population" = 11;
                """);
        }
    }
}

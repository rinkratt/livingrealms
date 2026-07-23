using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LivingRealms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RelocateStonehavenLumberYard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "ConstructionProjects",
                keyColumn: "Id",
                keyValue: new Guid("81000000-0000-4000-8000-000000000003"),
                columns: new[] { "PositionX", "PositionZ" },
                values: new object[] { -22f, -19.5f });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "living_realms",
                table: "ConstructionProjects",
                keyColumn: "Id",
                keyValue: new Guid("81000000-0000-4000-8000-000000000003"),
                columns: new[] { "PositionX", "PositionZ" },
                values: new object[] { -31f, -18f });
        }
    }
}

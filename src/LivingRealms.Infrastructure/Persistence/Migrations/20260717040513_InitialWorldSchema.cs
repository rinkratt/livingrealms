using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LivingRealms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialWorldSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "living_realms");

            migrationBuilder.CreateTable(
                name: "Accounts",
                schema: "living_realms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    IsAdministrator = table.Column<bool>(type: "boolean", nullable: false),
                    LastLoginAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CreatureSpecies",
                schema: "living_realms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    BaseHealth = table.Column<int>(type: "integer", nullable: false),
                    BaseAttack = table.Column<int>(type: "integer", nullable: false),
                    BaseDefense = table.Column<int>(type: "integer", nullable: false),
                    BaseMovementSpeed = table.Column<float>(type: "real", nullable: false),
                    IsPersistentByDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatureSpecies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Factions",
                schema: "living_realms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    LeaderCreatureId = table.Column<Guid>(type: "uuid", nullable: true),
                    Population = table.Column<int>(type: "integer", nullable: false),
                    TerritorySize = table.Column<int>(type: "integer", nullable: false),
                    Aggression = table.Column<int>(type: "integer", nullable: false),
                    Morale = table.Column<int>(type: "integer", nullable: false),
                    TechnologyLevel = table.Column<int>(type: "integer", nullable: false),
                    MilitaryStrength = table.Column<int>(type: "integer", nullable: false),
                    PopulationCapacity = table.Column<int>(type: "integer", nullable: false),
                    DevelopmentStage = table.Column<int>(type: "integer", nullable: false),
                    LastProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    NextDecisionAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Factions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Items",
                schema: "living_realms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    BaseValue = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Items", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Regions",
                schema: "living_realms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    ThreatLevel = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Regions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledEvents",
                schema: "living_realms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: true),
                    ScheduledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorldHistory",
                schema: "living_realms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    RegionId = table.Column<Guid>(type: "uuid", nullable: true),
                    FactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatureId = table.Column<Guid>(type: "uuid", nullable: true),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ImportanceLevel = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorldHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlayerSessions",
                schema: "living_realms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConnectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DisconnectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConnectionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerSessions_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "living_realms",
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FactionResources",
                schema: "living_realms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    Capacity = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FactionResources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FactionResources_Factions_FactionId",
                        column: x => x.FactionId,
                        principalSchema: "living_realms",
                        principalTable: "Factions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FactionStructures",
                schema: "living_realms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StructureType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Health = table.Column<int>(type: "integer", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FactionStructures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FactionStructures_Factions_FactionId",
                        column: x => x.FactionId,
                        principalSchema: "living_realms",
                        principalTable: "Factions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Characters",
                schema: "living_realms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Experience = table.Column<long>(type: "bigint", nullable: false),
                    Health = table.Column<int>(type: "integer", nullable: false),
                    MaximumHealth = table.Column<int>(type: "integer", nullable: false),
                    RegionId = table.Column<Guid>(type: "uuid", nullable: true),
                    PositionX = table.Column<float>(type: "real", nullable: false),
                    PositionY = table.Column<float>(type: "real", nullable: false),
                    PositionZ = table.Column<float>(type: "real", nullable: false),
                    LastLoginAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastLogoutAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Characters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Characters_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "living_realms",
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Characters_Regions_RegionId",
                        column: x => x.RegionId,
                        principalSchema: "living_realms",
                        principalTable: "Regions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Creatures",
                schema: "living_realms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SpeciesId = table.Column<Guid>(type: "uuid", nullable: false),
                    FactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    RegionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Experience = table.Column<long>(type: "bigint", nullable: false),
                    Health = table.Column<int>(type: "integer", nullable: false),
                    MaximumHealth = table.Column<int>(type: "integer", nullable: false),
                    Attack = table.Column<int>(type: "integer", nullable: false),
                    Defense = table.Column<int>(type: "integer", nullable: false),
                    MovementSpeed = table.Column<float>(type: "real", nullable: false),
                    Aggression = table.Column<int>(type: "integer", nullable: false),
                    Leadership = table.Column<int>(type: "integer", nullable: false),
                    Role = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    LastProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Creatures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Creatures_CreatureSpecies_SpeciesId",
                        column: x => x.SpeciesId,
                        principalSchema: "living_realms",
                        principalTable: "CreatureSpecies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Creatures_Factions_FactionId",
                        column: x => x.FactionId,
                        principalSchema: "living_realms",
                        principalTable: "Factions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Creatures_Regions_RegionId",
                        column: x => x.RegionId,
                        principalSchema: "living_realms",
                        principalTable: "Regions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Settlements",
                schema: "living_realms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RegionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Population = table.Column<int>(type: "integer", nullable: false),
                    StructuralIntegrity = table.Column<int>(type: "integer", nullable: false),
                    Food = table.Column<int>(type: "integer", nullable: false),
                    Wood = table.Column<int>(type: "integer", nullable: false),
                    Stone = table.Column<int>(type: "integer", nullable: false),
                    Iron = table.Column<int>(type: "integer", nullable: false),
                    DefenseRating = table.Column<int>(type: "integer", nullable: false),
                    GuardStrength = table.Column<int>(type: "integer", nullable: false),
                    LastAttackedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDestroyed = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settlements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Settlements_Regions_RegionId",
                        column: x => x.RegionId,
                        principalSchema: "living_realms",
                        principalTable: "Regions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CharacterInventory",
                schema: "living_realms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    IsEquipped = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterInventory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterInventory_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalSchema: "living_realms",
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CharacterInventory_Items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "living_realms",
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CreatureEquipment",
                schema: "living_realms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatureId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Slot = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatureEquipment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreatureEquipment_Creatures_CreatureId",
                        column: x => x.CreatureId,
                        principalSchema: "living_realms",
                        principalTable: "Creatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CreatureEquipment_Items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "living_realms",
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CreatureSkills",
                schema: "living_realms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatureId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Experience = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatureSkills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreatureSkills_Creatures_CreatureId",
                        column: x => x.CreatureId,
                        principalSchema: "living_realms",
                        principalTable: "Creatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_Email",
                schema: "living_realms",
                table: "Accounts",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CharacterInventory_CharacterId_ItemId",
                schema: "living_realms",
                table: "CharacterInventory",
                columns: new[] { "CharacterId", "ItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CharacterInventory_ItemId",
                schema: "living_realms",
                table: "CharacterInventory",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Characters_AccountId_Name",
                schema: "living_realms",
                table: "Characters",
                columns: new[] { "AccountId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Characters_RegionId",
                schema: "living_realms",
                table: "Characters",
                column: "RegionId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatureEquipment_CreatureId_Slot",
                schema: "living_realms",
                table: "CreatureEquipment",
                columns: new[] { "CreatureId", "Slot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreatureEquipment_ItemId",
                schema: "living_realms",
                table: "CreatureEquipment",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Creatures_FactionId",
                schema: "living_realms",
                table: "Creatures",
                column: "FactionId");

            migrationBuilder.CreateIndex(
                name: "IX_Creatures_RegionId_FactionId_Status",
                schema: "living_realms",
                table: "Creatures",
                columns: new[] { "RegionId", "FactionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Creatures_SpeciesId",
                schema: "living_realms",
                table: "Creatures",
                column: "SpeciesId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatureSkills_CreatureId_SkillKey",
                schema: "living_realms",
                table: "CreatureSkills",
                columns: new[] { "CreatureId", "SkillKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreatureSpecies_Key",
                schema: "living_realms",
                table: "CreatureSpecies",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FactionResources_FactionId_Kind",
                schema: "living_realms",
                table: "FactionResources",
                columns: new[] { "FactionId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Factions_Key",
                schema: "living_realms",
                table: "Factions",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FactionStructures_FactionId",
                schema: "living_realms",
                table: "FactionStructures",
                column: "FactionId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_Key",
                schema: "living_realms",
                table: "Items",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerSessions_AccountId_DisconnectedAt",
                schema: "living_realms",
                table: "PlayerSessions",
                columns: new[] { "AccountId", "DisconnectedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Regions_Key",
                schema: "living_realms",
                table: "Regions",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledEvents_IdempotencyKey",
                schema: "living_realms",
                table: "ScheduledEvents",
                column: "IdempotencyKey",
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledEvents_Status_ScheduledAt",
                schema: "living_realms",
                table: "ScheduledEvents",
                columns: new[] { "Status", "ScheduledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Settlements_RegionId_Name",
                schema: "living_realms",
                table: "Settlements",
                columns: new[] { "RegionId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorldHistory_OccurredAt",
                schema: "living_realms",
                table: "WorldHistory",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_WorldHistory_RegionId_OccurredAt",
                schema: "living_realms",
                table: "WorldHistory",
                columns: new[] { "RegionId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CharacterInventory",
                schema: "living_realms");

            migrationBuilder.DropTable(
                name: "CreatureEquipment",
                schema: "living_realms");

            migrationBuilder.DropTable(
                name: "CreatureSkills",
                schema: "living_realms");

            migrationBuilder.DropTable(
                name: "FactionResources",
                schema: "living_realms");

            migrationBuilder.DropTable(
                name: "FactionStructures",
                schema: "living_realms");

            migrationBuilder.DropTable(
                name: "PlayerSessions",
                schema: "living_realms");

            migrationBuilder.DropTable(
                name: "ScheduledEvents",
                schema: "living_realms");

            migrationBuilder.DropTable(
                name: "Settlements",
                schema: "living_realms");

            migrationBuilder.DropTable(
                name: "WorldHistory",
                schema: "living_realms");

            migrationBuilder.DropTable(
                name: "Characters",
                schema: "living_realms");

            migrationBuilder.DropTable(
                name: "Items",
                schema: "living_realms");

            migrationBuilder.DropTable(
                name: "Creatures",
                schema: "living_realms");

            migrationBuilder.DropTable(
                name: "Accounts",
                schema: "living_realms");

            migrationBuilder.DropTable(
                name: "CreatureSpecies",
                schema: "living_realms");

            migrationBuilder.DropTable(
                name: "Factions",
                schema: "living_realms");

            migrationBuilder.DropTable(
                name: "Regions",
                schema: "living_realms");
        }
    }
}

CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717040513_InitialWorldSchema') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'living_realms') THEN
            CREATE SCHEMA living_realms;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717040513_InitialWorldSchema') THEN
    CREATE TABLE living_realms."Accounts" (
        "Id" uuid NOT NULL,
        "Email" character varying(320) NOT NULL,
        "PasswordHash" character varying(512) NOT NULL,
        "IsAdministrator" boolean NOT NULL,
        "LastLoginAt" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        CONSTRAINT "PK_Accounts" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717040513_InitialWorldSchema') THEN
    CREATE TABLE living_realms."CreatureSpecies" (
        "Id" uuid NOT NULL,
        "Key" character varying(80) NOT NULL,
        "Name" character varying(120) NOT NULL,
        "BaseHealth" integer NOT NULL,
        "BaseAttack" integer NOT NULL,
        "BaseDefense" integer NOT NULL,
        "BaseMovementSpeed" real NOT NULL,
        "IsPersistentByDefault" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        CONSTRAINT "PK_CreatureSpecies" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717040513_InitialWorldSchema') THEN
    CREATE TABLE living_realms."Factions" (
        "Id" uuid NOT NULL,
        "Key" character varying(80) NOT NULL,
        "Name" character varying(120) NOT NULL,
        "LeaderCreatureId" uuid,
        "Population" integer NOT NULL,
        "TerritorySize" integer NOT NULL,
        "Aggression" integer NOT NULL,
        "Morale" integer NOT NULL,
        "TechnologyLevel" integer NOT NULL,
        "MilitaryStrength" integer NOT NULL,
        "PopulationCapacity" integer NOT NULL,
        "DevelopmentStage" integer NOT NULL,
        "LastProcessedAt" timestamp with time zone NOT NULL,
        "NextDecisionAt" timestamp with time zone NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        CONSTRAINT "PK_Factions" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717040513_InitialWorldSchema') THEN
    CREATE TABLE living_realms."Items" (
        "Id" uuid NOT NULL,
        "Key" character varying(80) NOT NULL,
        "Name" character varying(120) NOT NULL,
        "Description" text,
        "Kind" integer NOT NULL,
        "BaseValue" integer NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        CONSTRAINT "PK_Items" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717040513_InitialWorldSchema') THEN
    CREATE TABLE living_realms."Regions" (
        "Id" uuid NOT NULL,
        "Key" character varying(80) NOT NULL,
        "Name" character varying(120) NOT NULL,
        "Description" text,
        "ThreatLevel" integer NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        CONSTRAINT "PK_Regions" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717040513_InitialWorldSchema') THEN
    CREATE TABLE living_realms."ScheduledEvents" (
        "Id" uuid NOT NULL,
        "EventType" character varying(100) NOT NULL,
        "TargetId" uuid,
        "ScheduledAt" timestamp with time zone NOT NULL,
        "Status" integer NOT NULL,
        "StartedAt" timestamp with time zone,
        "CompletedAt" timestamp with time zone,
        "RetryCount" integer NOT NULL,
        "ErrorMessage" text,
        "IdempotencyKey" character varying(160),
        "PayloadJson" jsonb NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        CONSTRAINT "PK_ScheduledEvents" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717040513_InitialWorldSchema') THEN
    CREATE TABLE living_realms."WorldHistory" (
        "Id" uuid NOT NULL,
        "EventType" character varying(100) NOT NULL,
        "Title" character varying(200) NOT NULL,
        "Description" text NOT NULL,
        "RegionId" uuid,
        "FactionId" uuid,
        "CreatureId" uuid,
        "CharacterId" uuid,
        "OccurredAt" timestamp with time zone NOT NULL,
        "ImportanceLevel" integer NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        CONSTRAINT "PK_WorldHistory" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717040513_InitialWorldSchema') THEN
    CREATE TABLE living_realms."PlayerSessions" (
        "Id" uuid NOT NULL,
        "AccountId" uuid NOT NULL,
        "CharacterId" uuid,
        "ConnectedAt" timestamp with time zone NOT NULL,
        "DisconnectedAt" timestamp with time zone,
        "ConnectionId" character varying(128),
        "IpAddress" character varying(64),
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        CONSTRAINT "PK_PlayerSessions" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_PlayerSessions_Accounts_AccountId" FOREIGN KEY ("AccountId") REFERENCES living_realms."Accounts" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717040513_InitialWorldSchema') THEN
    CREATE TABLE living_realms."FactionResources" (
        "Id" uuid NOT NULL,
        "FactionId" uuid NOT NULL,
        "Kind" integer NOT NULL,
        "Amount" bigint NOT NULL,
        "Capacity" bigint NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        CONSTRAINT "PK_FactionResources" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_FactionResources_Factions_FactionId" FOREIGN KEY ("FactionId") REFERENCES living_realms."Factions" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717040513_InitialWorldSchema') THEN
    CREATE TABLE living_realms."FactionStructures" (
        "Id" uuid NOT NULL,
        "FactionId" uuid NOT NULL,
        "StructureType" character varying(80) NOT NULL,
        "Level" integer NOT NULL,
        "Health" integer NOT NULL,
        "CompletedAt" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        CONSTRAINT "PK_FactionStructures" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_FactionStructures_Factions_FactionId" FOREIGN KEY ("FactionId") REFERENCES living_realms."Factions" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717040513_InitialWorldSchema') THEN
    CREATE TABLE living_realms."Characters" (
        "Id" uuid NOT NULL,
        "AccountId" uuid NOT NULL,
        "Name" character varying(40) NOT NULL,
        "Level" integer NOT NULL,
        "Experience" bigint NOT NULL,
        "Health" integer NOT NULL,
        "MaximumHealth" integer NOT NULL,
        "RegionId" uuid,
        "PositionX" real NOT NULL,
        "PositionY" real NOT NULL,
        "PositionZ" real NOT NULL,
        "LastLoginAt" timestamp with time zone,
        "LastLogoutAt" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        CONSTRAINT "PK_Characters" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Characters_Accounts_AccountId" FOREIGN KEY ("AccountId") REFERENCES living_realms."Accounts" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_Characters_Regions_RegionId" FOREIGN KEY ("RegionId") REFERENCES living_realms."Regions" ("Id") ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717040513_InitialWorldSchema') THEN
    CREATE TABLE living_realms."Creatures" (
        "Id" uuid NOT NULL,
        "SpeciesId" uuid NOT NULL,
        "FactionId" uuid,
        "RegionId" uuid,
        "Name" character varying(120) NOT NULL,
        "Level" integer NOT NULL,
        "Experience" bigint NOT NULL,
        "Health" integer NOT NULL,
        "MaximumHealth" integer NOT NULL,
        "Attack" integer NOT NULL,
        "Defense" integer NOT NULL,
        "MovementSpeed" real NOT NULL,
        "Aggression" integer NOT NULL,
        "Leadership" integer NOT NULL,
        "Role" character varying(80),
        "Title" character varying(120),
        "Status" integer NOT NULL,
        "LastProcessedAt" timestamp with time zone NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        CONSTRAINT "PK_Creatures" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Creatures_CreatureSpecies_SpeciesId" FOREIGN KEY ("SpeciesId") REFERENCES living_realms."CreatureSpecies" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_Creatures_Factions_FactionId" FOREIGN KEY ("FactionId") REFERENCES living_realms."Factions" ("Id") ON DELETE SET NULL,
        CONSTRAINT "FK_Creatures_Regions_RegionId" FOREIGN KEY ("RegionId") REFERENCES living_realms."Regions" ("Id") ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717040513_InitialWorldSchema') THEN
    CREATE TABLE living_realms."Settlements" (
        "Id" uuid NOT NULL,
        "RegionId" uuid NOT NULL,
        "Name" character varying(120) NOT NULL,
        "Population" integer NOT NULL,
        "StructuralIntegrity" integer NOT NULL,
        "Food" integer NOT NULL,
        "Wood" integer NOT NULL,
        "Stone" integer NOT NULL,
        "Iron" integer NOT NULL,
        "DefenseRating" integer NOT NULL,
        "GuardStrength" integer NOT NULL,
        "LastAttackedAt" timestamp with time zone,
        "IsDestroyed" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        CONSTRAINT "PK_Settlements" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Settlements_Regions_RegionId" FOREIGN KEY ("RegionId") REFERENCES living_realms."Regions" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717040513_InitialWorldSchema') THEN
    CREATE TABLE living_realms."CharacterInventory" (
        "Id" uuid NOT NULL,
        "CharacterId" uuid NOT NULL,
        "ItemId" uuid NOT NULL,
        "Quantity" integer NOT NULL,
        "IsEquipped" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        CONSTRAINT "PK_CharacterInventory" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_CharacterInventory_Characters_CharacterId" FOREIGN KEY ("CharacterId") REFERENCES living_realms."Characters" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_CharacterInventory_Items_ItemId" FOREIGN KEY ("ItemId") REFERENCES living_realms."Items" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717040513_InitialWorldSchema') THEN
    CREATE TABLE living_realms."CreatureEquipment" (
        "Id" uuid NOT NULL,
        "CreatureId" uuid NOT NULL,
        "ItemId" uuid NOT NULL,
        "Slot" character varying(40) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        CONSTRAINT "PK_CreatureEquipment" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_CreatureEquipment_Creatures_CreatureId" FOREIGN KEY ("CreatureId") REFERENCES living_realms."Creatures" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_CreatureEquipment_Items_ItemId" FOREIGN KEY ("ItemId") REFERENCES living_realms."Items" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717040513_InitialWorldSchema') THEN
    CREATE TABLE living_realms."CreatureSkills" (
        "Id" uuid NOT NULL,
        "CreatureId" uuid NOT NULL,
        "SkillKey" character varying(80) NOT NULL,
        "Level" integer NOT NULL,
        "Experience" bigint NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        CONSTRAINT "PK_CreatureSkills" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_CreatureSkills_Creatures_CreatureId" FOREIGN KEY ("CreatureId") REFERENCES living_realms."Creatures" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717040513_InitialWorldSchema') THEN
    CREATE UNIQUE INDEX "IX_Accounts_Email" ON living_realms."Accounts" ("Email");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717040513_InitialWorldSchema') THEN
    CREATE UNIQUE INDEX "IX_CharacterInventory_CharacterId_ItemId" ON living_realms."CharacterInventory" ("CharacterId", "ItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717040513_InitialWorldSchema') THEN
    CREATE INDEX "IX_CharacterInventory_ItemId" ON living_realms."CharacterInventory" ("ItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717040513_InitialWorldSchema') THEN
    CREATE UNIQUE INDEX "IX_Characters_AccountId_Name" ON living_realms."Characters" ("AccountId", "Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717040513_InitialWorldSchema') THEN
    CREATE INDEX "IX_Characters_RegionId" ON living_realms."Characters" ("RegionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717040513_InitialWorldSchema') THEN
    CREATE UNIQUE INDEX "IX_CreatureEquipment_CreatureId_Slot" ON living_realms."CreatureEquipment" ("CreatureId", "Slot");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717040513_InitialWorldSchema') THEN
    CREATE INDEX "IX_CreatureEquipment_ItemId" ON living_realms."CreatureEquipment" ("ItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717040513_InitialWorldSchema') THEN
    CREATE INDEX "IX_Creatures_FactionId" ON living_realms."Creatures" ("FactionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717040513_InitialWorldSchema') THEN
    CREATE INDEX "IX_Creatures_RegionId_FactionId_Status" ON living_realms."Creatures" ("RegionId", "FactionId", "Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717040513_InitialWorldSchema') THEN
    CREATE INDEX "IX_Creatures_SpeciesId" ON living_realms."Creatures" ("SpeciesId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717040513_InitialWorldSchema') THEN
    CREATE UNIQUE INDEX "IX_CreatureSkills_CreatureId_SkillKey" ON living_realms."CreatureSkills" ("CreatureId", "SkillKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717040513_InitialWorldSchema') THEN
    CREATE UNIQUE INDEX "IX_CreatureSpecies_Key" ON living_realms."CreatureSpecies" ("Key");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717040513_InitialWorldSchema') THEN
    CREATE UNIQUE INDEX "IX_FactionResources_FactionId_Kind" ON living_realms."FactionResources" ("FactionId", "Kind");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717040513_InitialWorldSchema') THEN
    CREATE UNIQUE INDEX "IX_Factions_Key" ON living_realms."Factions" ("Key");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717040513_InitialWorldSchema') THEN
    CREATE INDEX "IX_FactionStructures_FactionId" ON living_realms."FactionStructures" ("FactionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717040513_InitialWorldSchema') THEN
    CREATE UNIQUE INDEX "IX_Items_Key" ON living_realms."Items" ("Key");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717040513_InitialWorldSchema') THEN
    CREATE INDEX "IX_PlayerSessions_AccountId_DisconnectedAt" ON living_realms."PlayerSessions" ("AccountId", "DisconnectedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717040513_InitialWorldSchema') THEN
    CREATE UNIQUE INDEX "IX_Regions_Key" ON living_realms."Regions" ("Key");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717040513_InitialWorldSchema') THEN
    CREATE UNIQUE INDEX "IX_ScheduledEvents_IdempotencyKey" ON living_realms."ScheduledEvents" ("IdempotencyKey") WHERE "IdempotencyKey" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717040513_InitialWorldSchema') THEN
    CREATE INDEX "IX_ScheduledEvents_Status_ScheduledAt" ON living_realms."ScheduledEvents" ("Status", "ScheduledAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717040513_InitialWorldSchema') THEN
    CREATE UNIQUE INDEX "IX_Settlements_RegionId_Name" ON living_realms."Settlements" ("RegionId", "Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717040513_InitialWorldSchema') THEN
    CREATE INDEX "IX_WorldHistory_OccurredAt" ON living_realms."WorldHistory" ("OccurredAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717040513_InitialWorldSchema') THEN
    CREATE INDEX "IX_WorldHistory_RegionId_OccurredAt" ON living_realms."WorldHistory" ("RegionId", "OccurredAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717040513_InitialWorldSchema') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260717040513_InitialWorldSchema', '8.0.11');
    END IF;
END $EF$;
COMMIT;
START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717042630_Phase2AccountsAndCharacters') THEN
    ALTER TABLE living_realms."PlayerSessions" ADD "ExpiresAt" timestamp with time zone NOT NULL DEFAULT TIMESTAMPTZ '-infinity';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717042630_Phase2AccountsAndCharacters') THEN
    ALTER TABLE living_realms."PlayerSessions" ADD "LastSeenAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717042630_Phase2AccountsAndCharacters') THEN
    ALTER TABLE living_realms."PlayerSessions" ADD "TokenHash" character varying(64);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717042630_Phase2AccountsAndCharacters') THEN
    ALTER TABLE living_realms."PlayerSessions" ADD "UserAgent" character varying(512);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717042630_Phase2AccountsAndCharacters') THEN
    ALTER TABLE living_realms."Characters" ADD "Archetype" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717042630_Phase2AccountsAndCharacters') THEN
    INSERT INTO living_realms."Regions" ("Id", "CreatedAt", "Description", "Key", "Name", "ThreatLevel", "UpdatedAt")
    VALUES ('7139a553-cea3-45e4-9d91-b3a95629b72e', TIMESTAMPTZ '2026-07-16T00:00:00+00:00', 'The first playable valley of Living Realms.', 'stonehaven-valley', 'Stonehaven Valley', 1, TIMESTAMPTZ '2026-07-16T00:00:00+00:00');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717042630_Phase2AccountsAndCharacters') THEN
    CREATE UNIQUE INDEX "IX_PlayerSessions_TokenHash" ON living_realms."PlayerSessions" ("TokenHash") WHERE "TokenHash" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717042630_Phase2AccountsAndCharacters') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260717042630_Phase2AccountsAndCharacters', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717125208_Phase4BasicMonstersAndCombat') THEN
    ALTER TABLE living_realms."CreatureSpecies" ADD "AttackRange" real NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717125208_Phase4BasicMonstersAndCombat') THEN
    ALTER TABLE living_realms."CreatureSpecies" ADD "DetectionRadius" real NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717125208_Phase4BasicMonstersAndCombat') THEN
    ALTER TABLE living_realms."CreatureSpecies" ADD "ExperienceReward" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717125208_Phase4BasicMonstersAndCombat') THEN
    ALTER TABLE living_realms."CreatureSpecies" ADD "RespawnSeconds" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717125208_Phase4BasicMonstersAndCombat') THEN
    ALTER TABLE living_realms."Creatures" ADD "LastAttackAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717125208_Phase4BasicMonstersAndCombat') THEN
    ALTER TABLE living_realms."Creatures" ADD "PositionX" real NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717125208_Phase4BasicMonstersAndCombat') THEN
    ALTER TABLE living_realms."Creatures" ADD "PositionY" real NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717125208_Phase4BasicMonstersAndCombat') THEN
    ALTER TABLE living_realms."Creatures" ADD "PositionZ" real NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717125208_Phase4BasicMonstersAndCombat') THEN
    ALTER TABLE living_realms."Creatures" ADD "RespawnAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717125208_Phase4BasicMonstersAndCombat') THEN
    ALTER TABLE living_realms."Creatures" ADD "SpawnX" real NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717125208_Phase4BasicMonstersAndCombat') THEN
    ALTER TABLE living_realms."Creatures" ADD "SpawnY" real NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717125208_Phase4BasicMonstersAndCombat') THEN
    ALTER TABLE living_realms."Creatures" ADD "SpawnZ" real NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717125208_Phase4BasicMonstersAndCombat') THEN
    ALTER TABLE living_realms."Characters" ADD "LastAttackAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717125208_Phase4BasicMonstersAndCombat') THEN
    INSERT INTO living_realms."CreatureSpecies" ("Id", "AttackRange", "BaseAttack", "BaseDefense", "BaseHealth", "BaseMovementSpeed", "CreatedAt", "DetectionRadius", "ExperienceReward", "IsPersistentByDefault", "Key", "Name", "RespawnSeconds", "UpdatedAt")
    VALUES ('5133411d-cb9d-4f00-a16e-ac106d7cfe91', 1.8, 15, 9, 90, 3.6, TIMESTAMPTZ '2026-07-16T00:00:00+00:00', 12, 90, TRUE, 'goblin-raider', 'Goblin Raider', 120, TIMESTAMPTZ '2026-07-16T00:00:00+00:00');
    INSERT INTO living_realms."CreatureSpecies" ("Id", "AttackRange", "BaseAttack", "BaseDefense", "BaseHealth", "BaseMovementSpeed", "CreatedAt", "DetectionRadius", "ExperienceReward", "IsPersistentByDefault", "Key", "Name", "RespawnSeconds", "UpdatedAt")
    VALUES ('5ff49fb8-b1db-4a5d-8274-8a0ee8ed4eb2', 1.7, 10, 5, 55, 4.2, TIMESTAMPTZ '2026-07-16T00:00:00+00:00', 10, 45, TRUE, 'prairie-wolf', 'Prairie Wolf', 75, TIMESTAMPTZ '2026-07-16T00:00:00+00:00');
    INSERT INTO living_realms."CreatureSpecies" ("Id", "AttackRange", "BaseAttack", "BaseDefense", "BaseHealth", "BaseMovementSpeed", "CreatedAt", "DetectionRadius", "ExperienceReward", "IsPersistentByDefault", "Key", "Name", "RespawnSeconds", "UpdatedAt")
    VALUES ('8ac9948d-3b09-4c70-aaf1-0c36f967c5a1', 1.35, 4, 2, 30, 3.2, TIMESTAMPTZ '2026-07-16T00:00:00+00:00', 7, 25, TRUE, 'forest-rat', 'Forest Rat', 45, TIMESTAMPTZ '2026-07-16T00:00:00+00:00');
    INSERT INTO living_realms."CreatureSpecies" ("Id", "AttackRange", "BaseAttack", "BaseDefense", "BaseHealth", "BaseMovementSpeed", "CreatedAt", "DetectionRadius", "ExperienceReward", "IsPersistentByDefault", "Key", "Name", "RespawnSeconds", "UpdatedAt")
    VALUES ('f3260673-96f8-4d56-ad45-25901cae6f98', 2.1, 22, 14, 180, 3.2, TIMESTAMPTZ '2026-07-16T00:00:00+00:00', 15, 220, TRUE, 'goblin-chief', 'Goblin Chief', 300, TIMESTAMPTZ '2026-07-16T00:00:00+00:00');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717125208_Phase4BasicMonstersAndCombat') THEN
    INSERT INTO living_realms."Creatures" ("Id", "Aggression", "Attack", "CreatedAt", "Defense", "Experience", "FactionId", "Health", "LastAttackAt", "LastProcessedAt", "Leadership", "Level", "MaximumHealth", "MovementSpeed", "Name", "PositionX", "PositionY", "PositionZ", "RegionId", "RespawnAt", "Role", "SpawnX", "SpawnY", "SpawnZ", "SpeciesId", "Status", "Title", "UpdatedAt")
    VALUES ('5d8a9637-a327-4f42-8ec3-a292f548d101', 45, 10, TIMESTAMPTZ '2026-07-16T00:00:00+00:00', 5, 0, NULL, 55, NULL, TIMESTAMPTZ '2026-07-16T00:00:00+00:00', 0, 2, 55, 4.2, 'Ashfang', -29, 0.08, 12, '7139a553-cea3-45e4-9d91-b3a95629b72e', NULL, 'Wild Creature', -29, 0.08, 12, '5ff49fb8-b1db-4a5d-8274-8a0ee8ed4eb2', 0, NULL, TIMESTAMPTZ '2026-07-16T00:00:00+00:00');
    INSERT INTO living_realms."Creatures" ("Id", "Aggression", "Attack", "CreatedAt", "Defense", "Experience", "FactionId", "Health", "LastAttackAt", "LastProcessedAt", "Leadership", "Level", "MaximumHealth", "MovementSpeed", "Name", "PositionX", "PositionY", "PositionZ", "RegionId", "RespawnAt", "Role", "SpawnX", "SpawnY", "SpawnZ", "SpeciesId", "Status", "Title", "UpdatedAt")
    VALUES ('5d8a9637-a327-4f42-8ec3-a292f548d102', 45, 10, TIMESTAMPTZ '2026-07-16T00:00:00+00:00', 5, 0, NULL, 55, NULL, TIMESTAMPTZ '2026-07-16T00:00:00+00:00', 0, 2, 55, 4.2, 'Dusthowl', 29, 0.08, 16, '7139a553-cea3-45e4-9d91-b3a95629b72e', NULL, 'Wild Creature', 29, 0.08, 16, '5ff49fb8-b1db-4a5d-8274-8a0ee8ed4eb2', 0, NULL, TIMESTAMPTZ '2026-07-16T00:00:00+00:00');
    INSERT INTO living_realms."Creatures" ("Id", "Aggression", "Attack", "CreatedAt", "Defense", "Experience", "FactionId", "Health", "LastAttackAt", "LastProcessedAt", "Leadership", "Level", "MaximumHealth", "MovementSpeed", "Name", "PositionX", "PositionY", "PositionZ", "RegionId", "RespawnAt", "Role", "SpawnX", "SpawnY", "SpawnZ", "SpeciesId", "Status", "Title", "UpdatedAt")
    VALUES ('8bd3a92f-80a8-46a6-8349-427975490a01', 20, 4, TIMESTAMPTZ '2026-07-16T00:00:00+00:00', 2, 0, NULL, 30, NULL, TIMESTAMPTZ '2026-07-16T00:00:00+00:00', 0, 1, 30, 3.2, 'Brambletail', -16, 0.08, 17, '7139a553-cea3-45e4-9d91-b3a95629b72e', NULL, 'Wild Creature', -16, 0.08, 17, '8ac9948d-3b09-4c70-aaf1-0c36f967c5a1', 0, NULL, TIMESTAMPTZ '2026-07-16T00:00:00+00:00');
    INSERT INTO living_realms."Creatures" ("Id", "Aggression", "Attack", "CreatedAt", "Defense", "Experience", "FactionId", "Health", "LastAttackAt", "LastProcessedAt", "Leadership", "Level", "MaximumHealth", "MovementSpeed", "Name", "PositionX", "PositionY", "PositionZ", "RegionId", "RespawnAt", "Role", "SpawnX", "SpawnY", "SpawnZ", "SpeciesId", "Status", "Title", "UpdatedAt")
    VALUES ('8bd3a92f-80a8-46a6-8349-427975490a02', 20, 4, TIMESTAMPTZ '2026-07-16T00:00:00+00:00', 2, 0, NULL, 30, NULL, TIMESTAMPTZ '2026-07-16T00:00:00+00:00', 0, 1, 30, 3.2, 'Mosswhisker', -8, 0.08, 34, '7139a553-cea3-45e4-9d91-b3a95629b72e', NULL, 'Wild Creature', -8, 0.08, 34, '8ac9948d-3b09-4c70-aaf1-0c36f967c5a1', 0, NULL, TIMESTAMPTZ '2026-07-16T00:00:00+00:00');
    INSERT INTO living_realms."Creatures" ("Id", "Aggression", "Attack", "CreatedAt", "Defense", "Experience", "FactionId", "Health", "LastAttackAt", "LastProcessedAt", "Leadership", "Level", "MaximumHealth", "MovementSpeed", "Name", "PositionX", "PositionY", "PositionZ", "RegionId", "RespawnAt", "Role", "SpawnX", "SpawnY", "SpawnZ", "SpeciesId", "Status", "Title", "UpdatedAt")
    VALUES ('8bd3a92f-80a8-46a6-8349-427975490a03', 20, 4, TIMESTAMPTZ '2026-07-16T00:00:00+00:00', 2, 0, NULL, 30, NULL, TIMESTAMPTZ '2026-07-16T00:00:00+00:00', 0, 1, 30, 3.2, 'Thornsnout', 12, 0.08, 32, '7139a553-cea3-45e4-9d91-b3a95629b72e', NULL, 'Wild Creature', 12, 0.08, 32, '8ac9948d-3b09-4c70-aaf1-0c36f967c5a1', 0, NULL, TIMESTAMPTZ '2026-07-16T00:00:00+00:00');
    INSERT INTO living_realms."Creatures" ("Id", "Aggression", "Attack", "CreatedAt", "Defense", "Experience", "FactionId", "Health", "LastAttackAt", "LastProcessedAt", "Leadership", "Level", "MaximumHealth", "MovementSpeed", "Name", "PositionX", "PositionY", "PositionZ", "RegionId", "RespawnAt", "Role", "SpawnX", "SpawnY", "SpawnZ", "SpeciesId", "Status", "Title", "UpdatedAt")
    VALUES ('9230414d-a60d-46ca-9c59-36cc3b867201', 70, 15, TIMESTAMPTZ '2026-07-16T00:00:00+00:00', 9, 0, NULL, 90, NULL, TIMESTAMPTZ '2026-07-16T00:00:00+00:00', 0, 5, 90, 3.6, 'Skrit', -23, 0.08, -39, '7139a553-cea3-45e4-9d91-b3a95629b72e', NULL, 'Wild Creature', -23, 0.08, -39, '5133411d-cb9d-4f00-a16e-ac106d7cfe91', 0, NULL, TIMESTAMPTZ '2026-07-16T00:00:00+00:00');
    INSERT INTO living_realms."Creatures" ("Id", "Aggression", "Attack", "CreatedAt", "Defense", "Experience", "FactionId", "Health", "LastAttackAt", "LastProcessedAt", "Leadership", "Level", "MaximumHealth", "MovementSpeed", "Name", "PositionX", "PositionY", "PositionZ", "RegionId", "RespawnAt", "Role", "SpawnX", "SpawnY", "SpawnZ", "SpeciesId", "Status", "Title", "UpdatedAt")
    VALUES ('9230414d-a60d-46ca-9c59-36cc3b867202', 70, 15, TIMESTAMPTZ '2026-07-16T00:00:00+00:00', 9, 0, NULL, 90, NULL, TIMESTAMPTZ '2026-07-16T00:00:00+00:00', 0, 5, 90, 3.6, 'Vrak', 22, 0.08, -39, '7139a553-cea3-45e4-9d91-b3a95629b72e', NULL, 'Wild Creature', 22, 0.08, -39, '5133411d-cb9d-4f00-a16e-ac106d7cfe91', 0, NULL, TIMESTAMPTZ '2026-07-16T00:00:00+00:00');
    INSERT INTO living_realms."Creatures" ("Id", "Aggression", "Attack", "CreatedAt", "Defense", "Experience", "FactionId", "Health", "LastAttackAt", "LastProcessedAt", "Leadership", "Level", "MaximumHealth", "MovementSpeed", "Name", "PositionX", "PositionY", "PositionZ", "RegionId", "RespawnAt", "Role", "SpawnX", "SpawnY", "SpawnZ", "SpeciesId", "Status", "Title", "UpdatedAt")
    VALUES ('f4c5a7b9-644f-4c85-b18f-ac38294e3001', 90, 22, TIMESTAMPTZ '2026-07-16T00:00:00+00:00', 14, 0, NULL, 180, NULL, TIMESTAMPTZ '2026-07-16T00:00:00+00:00', 0, 8, 180, 3.2, 'Gorvak', 0, 0.08, -41, '7139a553-cea3-45e4-9d91-b3a95629b72e', NULL, 'Chief', 0, 0.08, -41, 'f3260673-96f8-4d56-ad45-25901cae6f98', 0, 'Clan Chief', TIMESTAMPTZ '2026-07-16T00:00:00+00:00');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717125208_Phase4BasicMonstersAndCombat') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260717125208_Phase4BasicMonstersAndCombat', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717141233_Phase5LootEquipmentAndSkills') THEN
    ALTER TABLE living_realms."CharacterInventory" DROP CONSTRAINT "FK_CharacterInventory_Items_ItemId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717141233_Phase5LootEquipmentAndSkills') THEN
    ALTER TABLE living_realms."Items" ALTER COLUMN "Description" TYPE character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717141233_Phase5LootEquipmentAndSkills') THEN
    ALTER TABLE living_realms."Items" ADD "AttackBonus" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717141233_Phase5LootEquipmentAndSkills') THEN
    ALTER TABLE living_realms."Items" ADD "DefenseBonus" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717141233_Phase5LootEquipmentAndSkills') THEN
    ALTER TABLE living_realms."Items" ADD "EquipmentSlot" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717141233_Phase5LootEquipmentAndSkills') THEN
    ALTER TABLE living_realms."Items" ADD "HealingAmount" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717141233_Phase5LootEquipmentAndSkills') THEN
    ALTER TABLE living_realms."Items" ADD "Rarity" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717141233_Phase5LootEquipmentAndSkills') THEN
    ALTER TABLE living_realms."Items" ADD "RequiredArchetype" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717141233_Phase5LootEquipmentAndSkills') THEN
    CREATE TABLE living_realms."CharacterSkills" (
        "Id" uuid NOT NULL,
        "CharacterId" uuid NOT NULL,
        "SkillKey" character varying(80) NOT NULL,
        "Level" integer NOT NULL,
        "Experience" bigint NOT NULL,
        "LastUsedAt" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        CONSTRAINT "PK_CharacterSkills" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_CharacterSkills_Characters_CharacterId" FOREIGN KEY ("CharacterId") REFERENCES living_realms."Characters" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717141233_Phase5LootEquipmentAndSkills') THEN
    INSERT INTO living_realms."Items" ("Id", "AttackBonus", "BaseValue", "CreatedAt", "DefenseBonus", "Description", "EquipmentSlot", "HealingAmount", "Key", "Kind", "Name", "Rarity", "RequiredArchetype", "UpdatedAt")
    VALUES ('105a7b69-0e17-40d0-8d0f-4aa63bfb1001', 5, 35, TIMESTAMPTZ '2026-07-16T00:00:00+00:00', 0, 'A balanced iron blade issued to new vanguards.', 0, 0, 'stonehaven-training-blade', 1, 'Stonehaven Training Blade', 0, 0, TIMESTAMPTZ '2026-07-16T00:00:00+00:00');
    INSERT INTO living_realms."Items" ("Id", "AttackBonus", "BaseValue", "CreatedAt", "DefenseBonus", "Description", "EquipmentSlot", "HealingAmount", "Key", "Kind", "Name", "Rarity", "RequiredArchetype", "UpdatedAt")
    VALUES ('105a7b69-0e17-40d0-8d0f-4aa63bfb1002', 5, 35, TIMESTAMPTZ '2026-07-16T00:00:00+00:00', 0, 'A reliable yew bow issued to new rangers.', 0, 0, 'stonehaven-hunting-bow', 1, 'Stonehaven Hunting Bow', 0, 1, TIMESTAMPTZ '2026-07-16T00:00:00+00:00');
    INSERT INTO living_realms."Items" ("Id", "AttackBonus", "BaseValue", "CreatedAt", "DefenseBonus", "Description", "EquipmentSlot", "HealingAmount", "Key", "Kind", "Name", "Rarity", "RequiredArchetype", "UpdatedAt")
    VALUES ('105a7b69-0e17-40d0-8d0f-4aa63bfb1003', 0, 30, TIMESTAMPTZ '2026-07-16T00:00:00+00:00', 3, 'Layered leather that softens claws and rough blades.', 1, 0, 'stonehaven-leather-guard', 2, 'Stonehaven Leather Guard', 0, NULL, TIMESTAMPTZ '2026-07-16T00:00:00+00:00');
    INSERT INTO living_realms."Items" ("Id", "AttackBonus", "BaseValue", "CreatedAt", "DefenseBonus", "Description", "EquipmentSlot", "HealingAmount", "Key", "Kind", "Name", "Rarity", "RequiredArchetype", "UpdatedAt")
    VALUES ('105a7b69-0e17-40d0-8d0f-4aa63bfb1004', 0, 20, TIMESTAMPTZ '2026-07-16T00:00:00+00:00', 0, 'A sharp herbal draught that restores 35 health.', NULL, 35, 'field-tonic', 3, 'Field Tonic', 1, NULL, TIMESTAMPTZ '2026-07-16T00:00:00+00:00');
    INSERT INTO living_realms."Items" ("Id", "AttackBonus", "BaseValue", "CreatedAt", "DefenseBonus", "Description", "EquipmentSlot", "HealingAmount", "Key", "Kind", "Name", "Rarity", "RequiredArchetype", "UpdatedAt")
    VALUES ('105a7b69-0e17-40d0-8d0f-4aa63bfb1005', 0, 3, TIMESTAMPTZ '2026-07-16T00:00:00+00:00', 0, 'Proof that a Stonehaven field rat was defeated.', NULL, 0, 'forest-rat-tail', 4, 'Forest Rat Tail', 0, NULL, TIMESTAMPTZ '2026-07-16T00:00:00+00:00');
    INSERT INTO living_realms."Items" ("Id", "AttackBonus", "BaseValue", "CreatedAt", "DefenseBonus", "Description", "EquipmentSlot", "HealingAmount", "Key", "Kind", "Name", "Rarity", "RequiredArchetype", "UpdatedAt")
    VALUES ('105a7b69-0e17-40d0-8d0f-4aa63bfb1006', 0, 45, TIMESTAMPTZ '2026-07-16T00:00:00+00:00', 5, 'A thick pelt that can be equipped as light armor.', 1, 0, 'prairie-wolf-pelt', 2, 'Prairie Wolf Pelt', 1, NULL, TIMESTAMPTZ '2026-07-16T00:00:00+00:00');
    INSERT INTO living_realms."Items" ("Id", "AttackBonus", "BaseValue", "CreatedAt", "DefenseBonus", "Description", "EquipmentSlot", "HealingAmount", "Key", "Kind", "Name", "Rarity", "RequiredArchetype", "UpdatedAt")
    VALUES ('105a7b69-0e17-40d0-8d0f-4aa63bfb1007', 9, 80, TIMESTAMPTZ '2026-07-16T00:00:00+00:00', 0, 'A brutal but effective weapon recovered from a raider.', 0, 0, 'goblin-raider-blade', 1, 'Goblin Raider Blade', 1, 0, TIMESTAMPTZ '2026-07-16T00:00:00+00:00');
    INSERT INTO living_realms."Items" ("Id", "AttackBonus", "BaseValue", "CreatedAt", "DefenseBonus", "Description", "EquipmentSlot", "HealingAmount", "Key", "Kind", "Name", "Rarity", "RequiredArchetype", "UpdatedAt")
    VALUES ('105a7b69-0e17-40d0-8d0f-4aa63bfb1008', 8, 80, TIMESTAMPTZ '2026-07-16T00:00:00+00:00', 0, 'A horn-backed bow adapted for a Stonehaven ranger.', 0, 0, 'goblin-raider-bow', 1, 'Goblin Raider Bow', 1, 1, TIMESTAMPTZ '2026-07-16T00:00:00+00:00');
    INSERT INTO living_realms."Items" ("Id", "AttackBonus", "BaseValue", "CreatedAt", "DefenseBonus", "Description", "EquipmentSlot", "HealingAmount", "Key", "Kind", "Name", "Rarity", "RequiredArchetype", "UpdatedAt")
    VALUES ('105a7b69-0e17-40d0-8d0f-4aa63bfb1009', 14, 180, TIMESTAMPTZ '2026-07-16T00:00:00+00:00', 0, 'The heavy notched blade carried by the goblin chief.', 0, 0, 'gorvaks-warblade', 1, 'Gorvak''s Warblade', 2, 0, TIMESTAMPTZ '2026-07-16T00:00:00+00:00');
    INSERT INTO living_realms."Items" ("Id", "AttackBonus", "BaseValue", "CreatedAt", "DefenseBonus", "Description", "EquipmentSlot", "HealingAmount", "Key", "Kind", "Name", "Rarity", "RequiredArchetype", "UpdatedAt")
    VALUES ('105a7b69-0e17-40d0-8d0f-4aa63bfb1010', 13, 180, TIMESTAMPTZ '2026-07-16T00:00:00+00:00', 0, 'A captured warbow restrung for Elara''s reach.', 0, 0, 'gorvaks-warbow', 1, 'Gorvak''s Warbow', 2, 1, TIMESTAMPTZ '2026-07-16T00:00:00+00:00');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717141233_Phase5LootEquipmentAndSkills') THEN
    CREATE UNIQUE INDEX "IX_CharacterSkills_CharacterId_SkillKey" ON living_realms."CharacterSkills" ("CharacterId", "SkillKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717141233_Phase5LootEquipmentAndSkills') THEN
    ALTER TABLE living_realms."CharacterInventory" ADD CONSTRAINT "FK_CharacterInventory_Items_ItemId" FOREIGN KEY ("ItemId") REFERENCES living_realms."Items" ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717141233_Phase5LootEquipmentAndSkills') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260717141233_Phase5LootEquipmentAndSkills', '8.0.11');
    END IF;
END $EF$;
COMMIT;

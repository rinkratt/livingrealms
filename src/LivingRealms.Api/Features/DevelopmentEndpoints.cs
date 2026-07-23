using System.Security.Claims;
using LivingRealms.Api.Security;
using LivingRealms.Api.Time;
using LivingRealms.Domain.Entities;
using LivingRealms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LivingRealms.Api.Features;

public static class DevelopmentEndpoints
{
    private const float HarvestRange = 4.6f;
    private const int ContributionBundleSize = 10;

    public static IEndpointRouteBuilder MapDevelopmentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var development = endpoints.MapGroup("/api/v1/development").RequireAuthorization();
        development.MapGet("/state", GetStateAsync);
        development.MapPost("/harvest", HarvestAsync).RequireRateLimiting("gameplay");
        development.MapPost("/harvest-natural", HarvestNaturalAsync).RequireRateLimiting("gameplay");
        development.MapPost("/contribute", ContributeAsync).RequireRateLimiting("gameplay");
        development.MapPost("/npc-work", RecordNpcWorkAsync).RequireRateLimiting("gameplay");
        return endpoints;
    }

    private static async Task<IResult> GetStateAsync(
        HttpContext context,
        LivingRealmsDbContext database)
    {
        if (await GetSelectedCharacterAsync(context, database) is null)
        {
            return Results.Conflict(new ErrorResponse("Select a character before gathering resources."));
        }

        await RestoreReadyNodesAsync(database, DateTimeOffset.UtcNow, context.RequestAborted);
        return Results.Ok(await BuildStateAsync(database, context.RequestAborted));
    }

    private static async Task<IResult> HarvestAsync(
        HarvestRequest request,
        HttpContext context,
        LivingRealmsDbContext database)
    {
        var character = await GetSelectedCharacterAsync(context, database);
        if (character is null)
        {
            return Results.Conflict(new ErrorResponse("Select a character before gathering resources."));
        }
        if (!IsFinite(request.PlayerPosition.X) || !IsFinite(request.PlayerPosition.Y) ||
            !IsFinite(request.PlayerPosition.Z))
        {
            return Results.BadRequest(new ErrorResponse("The player position is invalid."));
        }

        var now = DateTimeOffset.UtcNow;
        var node = await database.WorldResourceNodes
            .SingleOrDefaultAsync(x => x.Id == request.NodeId, context.RequestAborted);
        if (node is null)
        {
            return Results.NotFound(new ErrorResponse("That resource node no longer exists."));
        }
        RestoreNodeIfReady(node, now);
        if (node.Remaining <= 0)
        {
            await database.SaveChangesAsync(context.RequestAborted);
            return Results.Conflict(new ErrorResponse(
                $"{node.Name} is depleted and will recover shortly."));
        }
        if (Distance(request.PlayerPosition, node) > HarvestRange)
        {
            return Results.BadRequest(new ErrorResponse($"Move closer to {node.Name} before gathering."));
        }
        if (character.LastGatherAt is not null && now - character.LastGatherAt.Value < TimeSpan.FromMilliseconds(850))
        {
            return Results.StatusCode(StatusCodes.Status429TooManyRequests);
        }

        var yieldProjectId = node.Kind == ResourceKind.Wood
            ? LivingRealmsDbContext.StonehavenLumberYardProjectId
            : LivingRealmsDbContext.StonehavenQuarryWorksProjectId;
        var yieldLevel = await database.ConstructionProjects.AsNoTracking()
            .Where(x => x.Id == yieldProjectId)
            .Select(x => x.CurrentLevel)
            .SingleAsync(context.RequestAborted);
        var requestedAmount = Math.Min(node.YieldPerHarvest + yieldLevel * 2, node.Remaining);
        var amount = await AddCarriedResourceAsync(
            character, node.Kind, requestedAmount, database, context.RequestAborted);
        if (amount <= 0)
        {
            return Results.Conflict(new ErrorResponse(
                "Your pack is full. Contribute materials to a project, sell supplies to Oren, or clear inventory space."));
        }
        node.Remaining -= amount;
        node.RespawnAt = node.Remaining == 0 ? now.AddSeconds(node.RespawnSeconds) : null;
        node.UpdatedAt = now;
        character.LastGatherAt = now;
        character.UpdatedAt = now;

        await database.SaveChangesAsync(context.RequestAborted);

        var verb = node.Kind == ResourceKind.Wood ? "chopped" : "mined";
        return Results.Ok(new DevelopmentActionResponse(
            await BuildStateAsync(database, context.RequestAborted),
            $"{character.Name} {verb} {amount} {node.Kind.ToString().ToLowerInvariant()} and placed it in the carried inventory. Take it to a project that needs it and press B."));
    }

    private static async Task<IResult> HarvestNaturalAsync(
        NaturalHarvestRequest request,
        HttpContext context,
        LivingRealmsDbContext database)
    {
        var character = await GetSelectedCharacterAsync(context, database);
        if (character is null)
        {
            return Results.Conflict(new ErrorResponse("Select a character before gathering resources."));
        }
        if (!Enum.TryParse<ResourceKind>(request.Kind, true, out var kind) ||
            kind is not (ResourceKind.Wood or ResourceKind.Stone))
        {
            return Results.BadRequest(new ErrorResponse("That natural object cannot be gathered."));
        }
        if (!IsValidWorldPosition(request.PlayerPosition) || !IsValidWorldPosition(request.ResourcePosition))
        {
            return Results.BadRequest(new ErrorResponse("The gathering position is invalid."));
        }
        if (Distance(request.PlayerPosition, request.ResourcePosition) > HarvestRange)
        {
            return Results.BadRequest(new ErrorResponse("Move closer before gathering that resource."));
        }

        var now = DateTimeOffset.UtcNow;
        if (character.LastGatherAt is not null && now - character.LastGatherAt.Value < TimeSpan.FromMilliseconds(850))
        {
            return Results.StatusCode(StatusCodes.Status429TooManyRequests);
        }
        var yieldProjectId = kind == ResourceKind.Wood
            ? LivingRealmsDbContext.StonehavenLumberYardProjectId
            : LivingRealmsDbContext.StonehavenQuarryWorksProjectId;
        var yieldLevel = await database.ConstructionProjects.AsNoTracking()
            .Where(x => x.Id == yieldProjectId)
            .Select(x => x.CurrentLevel)
            .SingleAsync(context.RequestAborted);
        var amount = await AddCarriedResourceAsync(
            character, kind, 3 + yieldLevel * 2, database, context.RequestAborted);
        if (amount <= 0)
        {
            return Results.Conflict(new ErrorResponse(
                "Your pack is full. Contribute materials to a project, sell supplies to Oren, or clear inventory space."));
        }

        character.LastGatherAt = now;
        character.UpdatedAt = now;
        await database.SaveChangesAsync(context.RequestAborted);
        return Results.Ok(new DevelopmentActionResponse(
            await BuildStateAsync(database, context.RequestAborted),
            $"Collected {amount} {kind.ToString().ToLowerInvariant()} into your carried inventory."));
    }

    private static async Task<IResult> ContributeAsync(
        ContributeRequest request,
        HttpContext context,
        LivingRealmsDbContext database)
    {
        var character = await GetSelectedCharacterAsync(context, database);
        if (character is null)
        {
            return Results.Conflict(new ErrorResponse("Select a character before contributing resources."));
        }
        var project = await database.ConstructionProjects
            .SingleOrDefaultAsync(x => x.Id == request.ProjectId, context.RequestAborted);
        if (project is null || project.Owner != ResourceOwner.Stonehaven)
        {
            return Results.BadRequest(new ErrorResponse("Players can only contribute to Stonehaven projects right now."));
        }
        if (Distance(request.PlayerPosition, project) > 6.5f)
        {
            return Results.BadRequest(new ErrorResponse($"Move closer to the {project.Name} construction marker."));
        }
        if (project.CompletedAt is not null)
        {
            return Results.Conflict(new ErrorResponse($"{project.Name} is already complete."));
        }
        var now = DateTimeOffset.UtcNow;
        if (character.LastContributionAt is not null &&
            now - character.LastContributionAt.Value < TimeSpan.FromMilliseconds(650))
        {
            return Results.StatusCode(StatusCodes.Status429TooManyRequests);
        }

        var carried = await database.CharacterInventory
            .Where(x => x.CharacterId == character.Id &&
                        (x.ItemId == LivingRealmsDbContext.TimberItemId ||
                         x.ItemId == LivingRealmsDbContext.RoughStoneItemId))
            .ToDictionaryAsync(x => x.ItemId, context.RequestAborted);
        carried.TryGetValue(LivingRealmsDbContext.TimberItemId, out var timberEntry);
        carried.TryGetValue(LivingRealmsDbContext.RoughStoneItemId, out var stoneEntry);
        var wood = Math.Min(
            ContributionBundleSize,
            Math.Min(timberEntry?.Quantity ?? 0, Math.Max(0, project.WoodRequired - project.WoodContributed)));
        var stone = Math.Min(
            ContributionBundleSize,
            Math.Min(stoneEntry?.Quantity ?? 0, Math.Max(0, project.StoneRequired - project.StoneContributed)));
        if (wood == 0 && stone == 0)
        {
            return Results.Conflict(new ErrorResponse(
                $"Your carried inventory does not contain materials {project.Name} still needs. " +
                $"Remaining: {Math.Max(0, project.WoodRequired - project.WoodContributed)} wood and " +
                $"{Math.Max(0, project.StoneRequired - project.StoneContributed)} stone."));
        }

        if (wood > 0)
        {
            RemoveInventoryQuantity(database, timberEntry!, wood);
            ApplyContribution(project, ResourceKind.Wood, wood, now);
            AddContribution(database, project.Id, character.Id, character.Name, ResourceKind.Wood, wood, "Player", now);
        }
        if (stone > 0)
        {
            RemoveInventoryQuantity(database, stoneEntry!, stone);
            ApplyContribution(project, ResourceKind.Stone, stone, now);
            AddContribution(database, project.Id, character.Id, character.Name, ResourceKind.Stone, stone, "Player", now);
        }
        var upgraded = await ApplyProjectUpgradeIfReadyAsync(
            database, project, character.Id, character.Name, now, context.RequestAborted);
        character.LastContributionAt = now;
        character.UpdatedAt = now;
        await database.SaveChangesAsync(context.RequestAborted);
        return Results.Ok(new DevelopmentActionResponse(
            await BuildStateAsync(database, context.RequestAborted),
            upgraded
                ? $"{character.Name} completed {project.Name} level {project.CurrentLevel}. Its world bonuses are now active."
                : $"{character.Name} contributed {wood} wood and {stone} stone to {project.Name}."));
    }

    private static async Task<IResult> RecordNpcWorkAsync(
        NpcWorkRequest request,
        HttpContext context,
        LivingRealmsDbContext database)
    {
        if (await GetSelectedCharacterAsync(context, database) is null)
        {
            return Results.Conflict(new ErrorResponse("Select a character before observing settlement work."));
        }

        var worker = ResolveWorker(request.WorkerKey);
        if (worker is null)
        {
            return Results.BadRequest(new ErrorResponse("That worker is not assigned to a construction crew."));
        }
        var node = await database.WorldResourceNodes
            .SingleOrDefaultAsync(x => x.Id == request.NodeId, context.RequestAborted);
        if (node is null || node.Owner != worker.Value.Owner || node.Kind != worker.Value.Kind)
        {
            return Results.BadRequest(new ErrorResponse("That resource does not match the worker's assignment."));
        }

        var now = DateTimeOffset.UtcNow;
        var lastWork = await database.ResourceContributions.AsNoTracking()
            .Where(x => x.ContributorName == worker.Value.DisplayName && x.Source == "NPC")
            .OrderByDescending(x => x.OccurredAt)
            .Select(x => (DateTimeOffset?)x.OccurredAt)
            .FirstOrDefaultAsync(context.RequestAborted);
        if (lastWork is not null && now - lastWork.Value < TimeSpan.FromSeconds(7))
        {
            return Results.StatusCode(StatusCodes.Status429TooManyRequests);
        }

        RestoreNodeIfReady(node, now);
        if (node.Remaining <= 0)
        {
            await database.SaveChangesAsync(context.RequestAborted);
            return Results.Conflict(new ErrorResponse($"{node.Name} is waiting to recover."));
        }

        var projectId = worker.Value.Owner == ResourceOwner.Stonehaven
            ? LivingRealmsDbContext.StonehavenWallProjectId
            : LivingRealmsDbContext.DarkwoodPalisadeProjectId;
        var project = await database.ConstructionProjects
            .SingleAsync(x => x.Id == projectId, context.RequestAborted);
        var needed = node.Kind == ResourceKind.Wood
            ? Math.Max(0, project.WoodRequired - project.WoodContributed)
            : Math.Max(0, project.StoneRequired - project.StoneContributed);
        var amount = Math.Min(needed, Math.Min(node.YieldPerHarvest, node.Remaining));
        if (amount <= 0)
        {
            return Results.Conflict(new ErrorResponse($"{project.Name} does not need more {node.Kind.ToString().ToLowerInvariant()} right now."));
        }
        node.Remaining -= amount;
        node.RespawnAt = node.Remaining == 0 ? now.AddSeconds(node.RespawnSeconds) : null;
        node.UpdatedAt = now;
        var delivered = ApplyContribution(project, node.Kind, amount, now);
        if (worker.Value.Owner == ResourceOwner.Stonehaven)
        {
            // Stonehaven workers carry their gathered bundle directly to the assigned project.
        }
        else
        {
            var resource = await database.FactionResources
                .SingleAsync(x => x.FactionId == LivingRealmsDbContext.DarkwoodClanId && x.Kind == node.Kind,
                    context.RequestAborted);
            resource.Amount = Math.Min(resource.Capacity, resource.Amount + delivered);
            resource.UpdatedAt = now;
        }
        AddContribution(database, project.Id, null, worker.Value.DisplayName, node.Kind, delivered, "NPC", now);
        await ApplyProjectUpgradeIfReadyAsync(
            database, project, null, worker.Value.DisplayName, now, context.RequestAborted);
        await database.SaveChangesAsync(context.RequestAborted);

        return Results.Ok(new DevelopmentActionResponse(
            await BuildStateAsync(database, context.RequestAborted),
            $"{worker.Value.DisplayName} delivered {delivered} {node.Kind.ToString().ToLowerInvariant()} to {project.Name}."));
    }

    private static WorkerAssignment? ResolveWorker(string workerKey) => workerKey.Trim().ToLowerInvariant() switch
    {
        "nessa" => new("Nessa", ResourceOwner.Stonehaven, ResourceKind.Wood),
        "dain" => new("Dain", ResourceOwner.Stonehaven, ResourceKind.Stone),
        "skrit" => new("Skrit", ResourceOwner.Darkwood, ResourceKind.Wood),
        "vrak" => new("Vrak", ResourceOwner.Darkwood, ResourceKind.Stone),
        _ => null
    };

    private static int ApplyContribution(
        ConstructionProject project,
        ResourceKind kind,
        int amount,
        DateTimeOffset now)
    {
        var contributed = kind switch
        {
            ResourceKind.Wood => Math.Min(amount, Math.Max(0, project.WoodRequired - project.WoodContributed)),
            ResourceKind.Stone => Math.Min(amount, Math.Max(0, project.StoneRequired - project.StoneContributed)),
            _ => 0
        };
        if (kind == ResourceKind.Wood)
        {
            project.WoodContributed += contributed;
        }
        else if (kind == ResourceKind.Stone)
        {
            project.StoneContributed += contributed;
        }
        project.UpdatedAt = now;
        return contributed;
    }

    private static void AddContribution(
        LivingRealmsDbContext database,
        Guid projectId,
        Guid? characterId,
        string contributorName,
        ResourceKind kind,
        int amount,
        string source,
        DateTimeOffset now)
    {
        database.ResourceContributions.Add(new ResourceContribution
        {
            ConstructionProjectId = projectId,
            CharacterId = characterId,
            ContributorName = contributorName,
            Kind = kind,
            Amount = amount,
            Source = source,
            OccurredAt = now,
            CreatedAt = now,
            UpdatedAt = now
        });
    }

    private static async Task<bool> ApplyProjectUpgradeIfReadyAsync(
        LivingRealmsDbContext database,
        ConstructionProject project,
        Guid? characterId,
        string contributorName,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (project.WoodContributed < project.WoodRequired ||
            project.StoneContributed < project.StoneRequired ||
            project.CurrentLevel >= project.MaximumLevel)
        {
            return false;
        }

        project.CurrentLevel++;
        if (project.Id == LivingRealmsDbContext.StonehavenWallProjectId)
        {
            var settlement = await database.Settlements
                .SingleAsync(x => x.Id == LivingRealmsDbContext.StonehavenVillageId, cancellationToken);
            settlement.DefenseRating += 12;
            settlement.GuardStrength += 4;
            settlement.StructuralIntegrity += 220;
            settlement.UpdatedAt = now;
        }
        else if (project.Id == LivingRealmsDbContext.DarkwoodPalisadeProjectId)
        {
            var faction = await database.Factions
                .SingleAsync(x => x.Id == LivingRealmsDbContext.DarkwoodClanId, cancellationToken);
            faction.MilitaryStrength += 14;
            faction.Morale = Math.Min(100, faction.Morale + 3);
            faction.UpdatedAt = now;
        }
        else if (project.Id == LivingRealmsDbContext.DarkwoodSupplyHutProjectId)
        {
            var resources = await database.FactionResources
                .Where(x => x.FactionId == LivingRealmsDbContext.DarkwoodClanId &&
                            (x.Kind == ResourceKind.Wood || x.Kind == ResourceKind.Stone))
                .ToListAsync(cancellationToken);
            foreach (var resource in resources)
            {
                resource.Capacity += 60;
                resource.UpdatedAt = now;
            }
        }

        var completed = project.CurrentLevel >= project.MaximumLevel;
        if (completed)
        {
            project.CompletedAt = now;
        }
        else
        {
            project.WoodContributed = 0;
            project.StoneContributed = 0;
            project.WoodRequired = (int)MathF.Ceiling(project.WoodRequired * 1.35f);
            project.StoneRequired = (int)MathF.Ceiling(project.StoneRequired * 1.35f);
        }
        project.UpdatedAt = now;
        database.WorldHistory.Add(new WorldHistory
        {
            EventType = completed ? "construction_completed" : "construction_upgraded",
            Title = completed
                ? $"{project.Name} reached its final tier"
                : $"{project.Name} reached level {project.CurrentLevel}",
            Description = $"{contributorName} supplied the final materials. {ProjectBenefit(project)}",
            RegionId = LivingRealmsDbContext.StonehavenValleyId,
            FactionId = project.FactionId,
            CharacterId = characterId,
            OccurredAt = now,
            ImportanceLevel = 3,
            CreatedAt = now,
            UpdatedAt = now
        });
        return true;
    }

    private static string ProjectBenefit(ConstructionProject project) => project.Key switch
    {
        "stonehaven-curtain-wall" => "Stonehaven gained defense, guard strength, and structural integrity.",
        "stonehaven-lumber-yard" => "Every player wood harvest now yields two additional wood per level.",
        "stonehaven-quarry-works" => "Every player stone harvest now yields two additional stone per level.",
        "darkwood-perimeter-palisade" => "Darkwood gained military strength and morale.",
        "darkwood-supply-hut" => "Darkwood expanded its wood and stone storage.",
        _ => "The settlement grew stronger."
    };

    private static async Task<int> AddCarriedResourceAsync(
        Character character,
        ResourceKind kind,
        int requestedAmount,
        LivingRealmsDbContext database,
        CancellationToken cancellationToken)
    {
        var itemId = kind == ResourceKind.Wood
            ? LivingRealmsDbContext.TimberItemId
            : LivingRealmsDbContext.RoughStoneItemId;
        var item = await database.Items.SingleAsync(x => x.Id == itemId, cancellationToken);
        var usedCapacity = await PhaseFiveEndpoints.GetCarriedWeightAsync(character.Id, database, cancellationToken);
        var freeCapacity = Math.Max(0, character.CarryCapacity - usedCapacity);
        var amount = Math.Min(requestedAmount, freeCapacity / Math.Max(1, item.UnitWeight));
        if (amount <= 0)
        {
            return 0;
        }

        var entry = await database.CharacterInventory
            .SingleOrDefaultAsync(x => x.CharacterId == character.Id && x.ItemId == itemId, cancellationToken);
        if (entry is null)
        {
            database.CharacterInventory.Add(new CharacterInventory
            {
                CharacterId = character.Id,
                ItemId = itemId,
                Quantity = amount,
                IsEquipped = false,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            entry.Quantity += amount;
            entry.UpdatedAt = DateTimeOffset.UtcNow;
        }
        return amount;
    }

    private static void RemoveInventoryQuantity(
        LivingRealmsDbContext database,
        CharacterInventory entry,
        int amount)
    {
        entry.Quantity -= amount;
        if (entry.Quantity <= 0)
        {
            database.CharacterInventory.Remove(entry);
        }
        else
        {
            entry.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    private static async Task RestoreReadyNodesAsync(
        LivingRealmsDbContext database,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var ready = await database.WorldResourceNodes
            .Where(x => x.Remaining <= 0 && x.RespawnAt != null && x.RespawnAt <= now)
            .ToListAsync(cancellationToken);
        foreach (var node in ready)
        {
            RestoreNodeIfReady(node, now);
        }
        if (ready.Count > 0)
        {
            await database.SaveChangesAsync(cancellationToken);
        }
    }

    private static void RestoreNodeIfReady(WorldResourceNode node, DateTimeOffset now)
    {
        if (node.Remaining <= 0 && node.RespawnAt is not null && node.RespawnAt <= now)
        {
            node.Remaining = node.Capacity;
            node.RespawnAt = null;
            node.UpdatedAt = now;
        }
    }

    private static async Task<DevelopmentStateResponse> BuildStateAsync(
        LivingRealmsDbContext database,
        CancellationToken cancellationToken)
    {
        var nodes = await database.WorldResourceNodes.AsNoTracking()
            .OrderBy(x => x.Owner).ThenBy(x => x.Kind).ThenBy(x => x.Name)
            .Select(x => new ResourceNodeResponse(
                x.Id, x.Key, x.Name, x.Kind.ToString(), x.Owner.ToString(),
                new PositionResponse(x.PositionX, x.PositionY, x.PositionZ),
                x.Remaining, x.Capacity, x.YieldPerHarvest, x.RespawnAt))
            .ToArrayAsync(cancellationToken);
        var projectRows = await database.ConstructionProjects.AsNoTracking()
            .OrderBy(x => x.Owner)
            .ToArrayAsync(cancellationToken);
        var projects = projectRows.Select(x => new ConstructionProjectResponse(
            x.Id, x.Key, x.Name, x.Owner.ToString(), x.WoodRequired, x.StoneRequired,
            x.WoodContributed, x.StoneContributed, x.CurrentLevel, x.MaximumLevel, ProjectProgress(x),
            ProjectStage(ProjectProgress(x)),
            new PositionResponse(x.PositionX, x.PositionY, x.PositionZ), x.CompletedAt)).ToArray();
        var contributions = await database.ResourceContributions.AsNoTracking()
            .OrderByDescending(x => x.OccurredAt)
            .Take(8)
            .Select(x => new ContributionResponse(
                x.ContributorName, x.Kind.ToString(), x.Amount, x.Source, x.OccurredAt))
            .ToArrayAsync(cancellationToken);
        var settlementStores = await database.Settlements.AsNoTracking()
            .Where(x => x.Id == LivingRealmsDbContext.StonehavenVillageId)
            .Select(x => new SettlementStoresResponse(x.Wood, x.Stone))
            .SingleAsync(cancellationToken);
        return new DevelopmentStateResponse(nodes, projects, contributions, settlementStores, CentralClock.Now);
    }

    private static float ProjectProgress(ConstructionProject project)
    {
        var required = project.WoodRequired + project.StoneRequired;
        if (project.CurrentLevel >= project.MaximumLevel)
        {
            return 1;
        }
        var tier = required <= 0
            ? 1
            : Math.Clamp((float)(project.WoodContributed + project.StoneContributed) / required, 0, 1);
        return Math.Clamp((project.CurrentLevel + tier) / Math.Max(1, project.MaximumLevel), 0, 1);
    }

    private static string ProjectStage(float progress) => progress switch
    {
        >= 1.0f => "Complete",
        >= 0.75f => "Finishing defenses",
        >= 0.50f => "Raising wall sections",
        >= 0.25f => "Building the frame",
        > 0 => "Laying foundations",
        _ => "Surveying"
    };

    private static async Task<Character?> GetSelectedCharacterAsync(
        HttpContext context,
        LivingRealmsDbContext database)
    {
        var accountId = GetRequiredId(context.User, ClaimTypes.NameIdentifier);
        var sessionId = GetRequiredId(context.User, SessionAuthenticationHandler.SessionIdClaim);
        var characterId = await database.PlayerSessions
            .Where(x => x.Id == sessionId && x.AccountId == accountId && x.DisconnectedAt == null)
            .Select(x => x.CharacterId)
            .SingleOrDefaultAsync(context.RequestAborted);
        return characterId is null
            ? null
            : await database.Characters.SingleOrDefaultAsync(
                x => x.Id == characterId.Value && x.AccountId == accountId,
                context.RequestAborted);
    }

    private static Guid GetRequiredId(ClaimsPrincipal principal, string claimType)
    {
        var value = principal.FindFirstValue(claimType);
        return Guid.TryParse(value, out var id)
            ? id
            : throw new InvalidOperationException($"The authenticated session is missing {claimType}.");
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool IsValidWorldPosition(PositionRequest position) =>
        IsFinite(position.X) && IsFinite(position.Y) && IsFinite(position.Z) &&
        MathF.Abs(position.X) <= 142 && MathF.Abs(position.Z) <= 142 && position.Y is >= -2 and <= 20;
    private static float Distance(PositionRequest first, PositionRequest second)
    {
        var x = first.X - second.X;
        var y = first.Y - second.Y;
        var z = first.Z - second.Z;
        return MathF.Sqrt(x * x + y * y + z * z);
    }
    private static float Distance(PositionRequest position, WorldResourceNode node)
    {
        var x = position.X - node.PositionX;
        var y = position.Y - node.PositionY;
        var z = position.Z - node.PositionZ;
        return MathF.Sqrt(x * x + y * y + z * z);
    }

    private static float Distance(PositionRequest position, ConstructionProject project)
    {
        var x = position.X - project.PositionX;
        var y = position.Y - project.PositionY;
        var z = position.Z - project.PositionZ;
        return MathF.Sqrt(x * x + y * y + z * z);
    }

    private readonly record struct WorkerAssignment(string DisplayName, ResourceOwner Owner, ResourceKind Kind);
    private sealed record ErrorResponse(string Error);
    public sealed record PositionRequest(float X, float Y, float Z);
    public sealed record HarvestRequest(Guid NodeId, PositionRequest PlayerPosition);
    public sealed record NaturalHarvestRequest(
        string Kind, PositionRequest ResourcePosition, PositionRequest PlayerPosition);
    public sealed record ContributeRequest(Guid ProjectId, PositionRequest PlayerPosition);
    public sealed record NpcWorkRequest(string WorkerKey, Guid NodeId);
    public sealed record DevelopmentActionResponse(DevelopmentStateResponse State, string Message);
    public sealed record DevelopmentStateResponse(
        IReadOnlyCollection<ResourceNodeResponse> Nodes,
        IReadOnlyCollection<ConstructionProjectResponse> Projects,
        IReadOnlyCollection<ContributionResponse> RecentContributions,
        SettlementStoresResponse SettlementStores,
        DateTimeOffset ServerTimeCentral);
    public sealed record ResourceNodeResponse(
        Guid Id, string Key, string Name, string Kind, string Owner, PositionResponse Position,
        int Remaining, int Capacity, int YieldPerHarvest, DateTimeOffset? RespawnAt);
    public sealed record ConstructionProjectResponse(
        Guid Id, string Key, string Name, string Owner,
        int WoodRequired, int StoneRequired, int WoodContributed, int StoneContributed,
        int CurrentLevel, int MaximumLevel, float Progress, string Stage,
        PositionResponse Position, DateTimeOffset? CompletedAt);
    public sealed record SettlementStoresResponse(int Wood, int Stone);
    public sealed record ContributionResponse(
        string ContributorName, string Kind, int Amount, string Source, DateTimeOffset OccurredAt);
    public sealed record PositionResponse(float X, float Y, float Z);
}

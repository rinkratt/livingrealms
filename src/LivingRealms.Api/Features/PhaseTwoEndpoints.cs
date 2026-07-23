using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using LivingRealms.Api.Security;
using LivingRealms.Api.Time;
using LivingRealms.Api.Logging;
using LivingRealms.Domain.Entities;
using LivingRealms.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LivingRealms.Api.Features;

public static class PhaseTwoEndpoints
{
    private const float MaximumCoordinate = 100_000;

    public static IEndpointRouteBuilder MapPhaseTwoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api/v1");

        api.MapPost("/accounts/register", RegisterAsync)
            .RequireRateLimiting("authentication");
        api.MapPost("/auth/login", LoginAsync)
            .RequireRateLimiting("authentication");

        var authenticated = api.MapGroup(string.Empty).RequireAuthorization();
        authenticated.MapPost("/auth/logout", LogoutAsync);
        authenticated.MapGet("/characters", ListCharactersAsync);
        authenticated.MapGet("/characters/current", GetCurrentCharacterAsync);
        authenticated.MapPost("/characters/{characterId:guid}/select", SelectCharacterAsync);
        authenticated.MapPut("/characters/{characterId:guid}/position", SavePositionAsync)
            .RequireRateLimiting("gameplay");

        return endpoints;
    }

    private static async Task<IResult> RegisterAsync(
        RegistrationRequest request,
        HttpContext context,
        LivingRealmsDbContext database,
        IPasswordHasher<Account> passwordHasher,
        IConfiguration configuration,
        ILoggerFactory loggerFactory)
    {
        var validation = ValidateCredentials(request.Email, request.Password);
        if (validation.Count > 0)
        {
            return Results.ValidationProblem(validation);
        }

        var email = NormalizeEmail(request.Email);
        if (await database.Accounts.AnyAsync(x => x.Email == email, context.RequestAborted))
        {
            return Results.Conflict(new ErrorResponse("An account with that email already exists."));
        }

        var regionExists = await database.Regions.AnyAsync(
            x => x.Id == LivingRealmsDbContext.StonehavenValleyId,
            context.RequestAborted);
        if (!regionExists)
        {
            return Results.Problem(
                "Stonehaven Valley has not been initialized. Apply the Phase 2 migration before registering players.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var now = DateTimeOffset.UtcNow;
        var account = new Account
        {
            Email = email,
            PasswordHash = string.Empty,
            CreatedAt = now,
            UpdatedAt = now
        };
        account.PasswordHash = passwordHasher.HashPassword(account, request.Password);

        account.Characters.Add(CreateCharacter(account.Id, "Alden", CharacterArchetype.Vanguard, -2, now));
        account.Characters.Add(CreateCharacter(account.Id, "Elara", CharacterArchetype.Ranger, 2, now));

        var (rawToken, session) = CreateSession(account, context, configuration, now);
        account.Sessions.Add(session);
        database.Accounts.Add(account);

        try
        {
            await database.SaveChangesAsync(context.RequestAborted);
        }
        catch (DbUpdateException)
        {
            return Results.Conflict(new ErrorResponse("An account with that email already exists."));
        }

        var logger = loggerFactory.CreateLogger("LivingRealms.Audit");
        AuditLog.AccountRegistered(
            logger,
            account.Id,
            session.IpAddress,
            CentralClock.Now);

        var response = CreateAuthenticationResponse(account, session, rawToken);
        return Results.Created("/api/v1/characters", response);
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        HttpContext context,
        LivingRealmsDbContext database,
        IPasswordHasher<Account> passwordHasher,
        IConfiguration configuration,
        ILoggerFactory loggerFactory)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["credentials"] = ["Email and password are required."]
            });
        }

        var logger = loggerFactory.CreateLogger("LivingRealms.Audit");
        var email = NormalizeEmail(request.Email);
        var account = await database.Accounts
            .Include(x => x.Characters)
            .SingleOrDefaultAsync(x => x.Email == email, context.RequestAborted);

        if (account is null)
        {
            var dummyAccount = new Account { Email = "missing@example.invalid", PasswordHash = string.Empty };
            _ = passwordHasher.HashPassword(dummyAccount, request.Password);
            LogFailedLogin(logger, context);
            return Results.Unauthorized();
        }

        var verification = passwordHasher.VerifyHashedPassword(account, account.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            LogFailedLogin(logger, context);
            return Results.Unauthorized();
        }

        var now = DateTimeOffset.UtcNow;
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            account.PasswordHash = passwordHasher.HashPassword(account, request.Password);
        }

        account.LastLoginAt = now;
        account.UpdatedAt = now;
        var (rawToken, session) = CreateSession(account, context, configuration, now);
        database.PlayerSessions.Add(session);
        await database.SaveChangesAsync(context.RequestAborted);

        AuditLog.AccountLoggedIn(
            logger,
            account.Id,
            session.Id,
            session.IpAddress,
            CentralClock.Now);

        return Results.Ok(CreateAuthenticationResponse(account, session, rawToken));
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext context,
        LivingRealmsDbContext database,
        ILoggerFactory loggerFactory)
    {
        var sessionId = GetRequiredId(context.User, SessionAuthenticationHandler.SessionIdClaim);
        var session = await database.PlayerSessions.FindAsync([sessionId], context.RequestAborted);
        if (session is null)
        {
            return Results.Unauthorized();
        }

        var now = DateTimeOffset.UtcNow;
        session.DisconnectedAt = now;
        session.LastSeenAt = now;
        session.UpdatedAt = now;
        await database.SaveChangesAsync(context.RequestAborted);

        var logger = loggerFactory.CreateLogger("LivingRealms.Audit");
        AuditLog.AccountLoggedOut(
            logger,
            session.AccountId,
            session.Id,
            CentralClock.Now);
        return Results.NoContent();
    }

    private static async Task<IResult> ListCharactersAsync(
        HttpContext context,
        LivingRealmsDbContext database)
    {
        var accountId = GetRequiredId(context.User, ClaimTypes.NameIdentifier);
        var characters = await database.Characters
            .AsNoTracking()
            .Include(x => x.Region)
            .Where(x => x.AccountId == accountId)
            .OrderBy(x => x.Name)
            .ToListAsync(context.RequestAborted);

        return Results.Ok(characters.Select(ToResponse));
    }

    private static async Task<IResult> GetCurrentCharacterAsync(
        HttpContext context,
        LivingRealmsDbContext database)
    {
        var accountId = GetRequiredId(context.User, ClaimTypes.NameIdentifier);
        var sessionId = GetRequiredId(context.User, SessionAuthenticationHandler.SessionIdClaim);
        var character = await database.PlayerSessions
            .Where(x => x.Id == sessionId && x.AccountId == accountId && x.CharacterId != null)
            .Select(x => x.CharacterId)
            .Join(database.Characters.Include(x => x.Region), id => id, character => character.Id, (_, character) => character)
            .AsNoTracking()
            .SingleOrDefaultAsync(context.RequestAborted);

        return character is null
            ? Results.NotFound(new ErrorResponse("No character is selected for this session."))
            : Results.Ok(ToResponse(character));
    }

    private static async Task<IResult> SelectCharacterAsync(
        Guid characterId,
        HttpContext context,
        LivingRealmsDbContext database,
        ILoggerFactory loggerFactory)
    {
        var accountId = GetRequiredId(context.User, ClaimTypes.NameIdentifier);
        var character = await database.Characters
            .Include(x => x.Region)
            .SingleOrDefaultAsync(
                x => x.Id == characterId && x.AccountId == accountId,
                context.RequestAborted);
        if (character is null)
        {
            return Results.NotFound(new ErrorResponse("Character not found."));
        }

        var sessionId = GetRequiredId(context.User, SessionAuthenticationHandler.SessionIdClaim);
        var session = await database.PlayerSessions.FindAsync([sessionId], context.RequestAborted);
        if (session is null || session.AccountId != accountId)
        {
            return Results.Unauthorized();
        }

        var now = DateTimeOffset.UtcNow;
        session.CharacterId = character.Id;
        session.LastSeenAt = now;
        session.UpdatedAt = now;
        character.LastLoginAt = now;
        character.UpdatedAt = now;
        await database.SaveChangesAsync(context.RequestAborted);

        var logger = loggerFactory.CreateLogger("LivingRealms.Audit");
        AuditLog.CharacterSelected(
            logger,
            accountId,
            character.Id,
            session.Id,
            CentralClock.Now);
        return Results.Ok(ToResponse(character));
    }

    private static async Task<IResult> SavePositionAsync(
        Guid characterId,
        PositionRequest request,
        HttpContext context,
        LivingRealmsDbContext database,
        ILoggerFactory loggerFactory)
    {
        if (!IsValidCoordinate(request.X) || !IsValidCoordinate(request.Y) || !IsValidCoordinate(request.Z))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["position"] = [$"Coordinates must be finite and between {-MaximumCoordinate} and {MaximumCoordinate}."]
            });
        }

        var accountId = GetRequiredId(context.User, ClaimTypes.NameIdentifier);
        var sessionId = GetRequiredId(context.User, SessionAuthenticationHandler.SessionIdClaim);
        var session = await database.PlayerSessions.FindAsync([sessionId], context.RequestAborted);
        if (session?.CharacterId != characterId)
        {
            return Results.Conflict(new ErrorResponse("Select this character before saving its position."));
        }

        var character = await database.Characters
            .Include(x => x.Region)
            .SingleOrDefaultAsync(
                x => x.Id == characterId && x.AccountId == accountId,
                context.RequestAborted);
        if (character is null)
        {
            return Results.NotFound(new ErrorResponse("Character not found."));
        }

        var now = DateTimeOffset.UtcNow;
        character.PositionX = request.X;
        character.PositionY = request.Y;
        character.PositionZ = request.Z;
        character.UpdatedAt = now;
        session.LastSeenAt = now;
        session.UpdatedAt = now;
        await database.SaveChangesAsync(context.RequestAborted);

        var logger = loggerFactory.CreateLogger("LivingRealms.Audit");
        AuditLog.PositionSaved(
            logger,
            character.Id,
            request.X,
            request.Y,
            request.Z,
            accountId,
            CentralClock.Now);
        return Results.Ok(ToResponse(character));
    }

    private static Dictionary<string, string[]> ValidateCredentials(string email, string password)
    {
        var errors = new Dictionary<string, string[]>();
        var normalizedEmail = email?.Trim() ?? string.Empty;
        if (normalizedEmail.Length > 320 || !new EmailAddressAttribute().IsValid(normalizedEmail))
        {
            errors["email"] = ["Enter a valid email address no longer than 320 characters."];
        }

        var passwordErrors = new List<string>();
        if (password is null || password.Length is < 12 or > 128)
        {
            passwordErrors.Add("Password must contain between 12 and 128 characters.");
        }
        else
        {
            if (!password.Any(char.IsUpper)) passwordErrors.Add("Password must contain an uppercase letter.");
            if (!password.Any(char.IsLower)) passwordErrors.Add("Password must contain a lowercase letter.");
            if (!password.Any(char.IsDigit)) passwordErrors.Add("Password must contain a number.");
            if (!password.Any(character => !char.IsLetterOrDigit(character))) passwordErrors.Add("Password must contain a symbol.");
        }

        if (passwordErrors.Count > 0)
        {
            errors["password"] = [.. passwordErrors];
        }

        return errors;
    }

    private static (string RawToken, PlayerSession Session) CreateSession(
        Account account,
        HttpContext context,
        IConfiguration configuration,
        DateTimeOffset now)
    {
        var rawToken = SessionToken.Create();
        var sessionHours = Math.Clamp(configuration.GetValue("Authentication:SessionHours", 12), 1, 168);
        var userAgent = context.Request.Headers.UserAgent.ToString();
        if (userAgent.Length > 512)
        {
            userAgent = userAgent[..512];
        }

        var session = new PlayerSession
        {
            AccountId = account.Id,
            Account = account,
            ConnectedAt = now,
            ExpiresAt = now.AddHours(sessionHours),
            LastSeenAt = now,
            TokenHash = SessionToken.Hash(rawToken),
            IpAddress = context.Connection.RemoteIpAddress?.ToString(),
            UserAgent = userAgent,
            CreatedAt = now,
            UpdatedAt = now
        };
        return (rawToken, session);
    }

    private static Character CreateCharacter(
        Guid accountId,
        string name,
        CharacterArchetype archetype,
        float positionX,
        DateTimeOffset now)
    {
        return new Character
        {
            AccountId = accountId,
            Name = name,
            Archetype = archetype,
            RegionId = LivingRealmsDbContext.StonehavenValleyId,
            PositionX = positionX,
            PositionY = 0,
            PositionZ = 0,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static AuthenticationResponse CreateAuthenticationResponse(
        Account account,
        PlayerSession session,
        string rawToken)
    {
        return new AuthenticationResponse(
            rawToken,
            session.ExpiresAt,
            new AccountResponse(account.Id, account.Email, account.IsAdministrator),
            account.Characters.OrderBy(x => x.Name).Select(ToResponse).ToArray());
    }

    private static CharacterResponse ToResponse(Character character)
    {
        return new CharacterResponse(
            character.Id,
            character.Name,
            character.Archetype.ToString(),
            character.Level,
            character.Experience,
            character.Health,
            character.MaximumHealth,
            character.Region?.Name ?? "Stonehaven Valley",
            new PositionResponse(character.PositionX, character.PositionY, character.PositionZ),
            character.UpdatedAt);
    }

    private static Guid GetRequiredId(ClaimsPrincipal user, string claimType)
    {
        var value = user.FindFirstValue(claimType);
        return Guid.TryParse(value, out var id)
            ? id
            : throw new InvalidOperationException($"Authenticated principal is missing the {claimType} claim.");
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static bool IsValidCoordinate(float value) =>
        float.IsFinite(value) && value is >= -MaximumCoordinate and <= MaximumCoordinate;

    private static void LogFailedLogin(ILogger logger, HttpContext context)
    {
        AuditLog.LoginRejected(
            logger,
            context.Connection.RemoteIpAddress?.ToString(),
            context.Request.Headers.UserAgent.ToString(),
            CentralClock.Now);
    }

    public sealed record RegistrationRequest(string Email, string Password);
    public sealed record LoginRequest(string Email, string Password);
    public sealed record PositionRequest(float X, float Y, float Z);
    public sealed record ErrorResponse(string Error);
    public sealed record AccountResponse(Guid Id, string Email, bool IsAdministrator);
    public sealed record PositionResponse(float X, float Y, float Z);
    public sealed record CharacterResponse(
        Guid Id,
        string Name,
        string Archetype,
        int Level,
        long Experience,
        int Health,
        int MaximumHealth,
        string Region,
        PositionResponse Position,
        DateTimeOffset UpdatedAt);
    public sealed record AuthenticationResponse(
        string Token,
        DateTimeOffset ExpiresAt,
        AccountResponse Account,
        IReadOnlyCollection<CharacterResponse> Characters);
}

namespace LivingRealms.Api.Logging;

public static partial class AuditLog
{
    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Information,
        Message = "Player account {AccountId} registered from {IpAddress} at {CentralTime}")]
    public static partial void AccountRegistered(
        ILogger logger,
        Guid accountId,
        string? ipAddress,
        DateTimeOffset centralTime);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Information,
        Message = "Player account {AccountId} logged in with session {SessionId} from {IpAddress} at {CentralTime}")]
    public static partial void AccountLoggedIn(
        ILogger logger,
        Guid accountId,
        Guid sessionId,
        string? ipAddress,
        DateTimeOffset centralTime);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Warning,
        Message = "Rejected player login from {IpAddress} using {UserAgent} at {CentralTime}")]
    public static partial void LoginRejected(
        ILogger logger,
        string? ipAddress,
        string userAgent,
        DateTimeOffset centralTime);

    [LoggerMessage(
        EventId = 2004,
        Level = LogLevel.Information,
        Message = "Player account {AccountId} logged out of session {SessionId} at {CentralTime}")]
    public static partial void AccountLoggedOut(
        ILogger logger,
        Guid accountId,
        Guid sessionId,
        DateTimeOffset centralTime);

    [LoggerMessage(
        EventId = 2005,
        Level = LogLevel.Information,
        Message = "Player account {AccountId} selected character {CharacterId} in session {SessionId} at {CentralTime}")]
    public static partial void CharacterSelected(
        ILogger logger,
        Guid accountId,
        Guid characterId,
        Guid sessionId,
        DateTimeOffset centralTime);

    [LoggerMessage(
        EventId = 2006,
        Level = LogLevel.Information,
        Message = "Character {CharacterId} position saved at {X}, {Y}, {Z} by account {AccountId} at {CentralTime}")]
    public static partial void PositionSaved(
        ILogger logger,
        Guid characterId,
        float x,
        float y,
        float z,
        Guid accountId,
        DateTimeOffset centralTime);

    [LoggerMessage(
        EventId = 2007,
        Level = LogLevel.Information,
        Message = "Character {CharacterId} dealt {Damage} damage to creature {CreatureId} by account {AccountId} at {CentralTime}")]
    public static partial void CreatureDamaged(
        ILogger logger,
        Guid characterId,
        Guid creatureId,
        int damage,
        Guid accountId,
        DateTimeOffset centralTime);

    [LoggerMessage(
        EventId = 2008,
        Level = LogLevel.Information,
        Message = "Character {CharacterId} defeated creature {CreatureId}, gained {Experience} experience, and reached level {Level} by account {AccountId} at {CentralTime}")]
    public static partial void CreatureDefeated(
        ILogger logger,
        Guid characterId,
        Guid creatureId,
        int experience,
        int level,
        Guid accountId,
        DateTimeOffset centralTime);

    [LoggerMessage(
        EventId = 2009,
        Level = LogLevel.Information,
        Message = "Creature {CreatureId} dealt {Damage} damage to character {CharacterId} by account {AccountId} at {CentralTime}")]
    public static partial void CharacterDamaged(
        ILogger logger,
        Guid creatureId,
        Guid characterId,
        int damage,
        Guid accountId,
        DateTimeOffset centralTime);

    [LoggerMessage(
        EventId = 2010,
        Level = LogLevel.Warning,
        Message = "Character {CharacterId} was knocked out by creature {CreatureId} for account {AccountId} at {CentralTime}")]
    public static partial void CharacterKnockedOut(
        ILogger logger,
        Guid characterId,
        Guid creatureId,
        Guid accountId,
        DateTimeOffset centralTime);

    [LoggerMessage(
        EventId = 2011,
        Level = LogLevel.Information,
        Message = "Character {CharacterId} equipped item {ItemId} ({ItemName}) by account {AccountId} at {CentralTime}")]
    public static partial void ItemEquipped(
        ILogger logger,
        Guid characterId,
        Guid itemId,
        string itemName,
        Guid accountId,
        DateTimeOffset centralTime);

    [LoggerMessage(
        EventId = 2012,
        Level = LogLevel.Information,
        Message = "Character {CharacterId} used item {ItemId} and restored {Healing} health by account {AccountId} at {CentralTime}")]
    public static partial void ItemUsed(
        ILogger logger,
        Guid characterId,
        Guid itemId,
        int healing,
        Guid accountId,
        DateTimeOffset centralTime);

    [LoggerMessage(
        EventId = 2013,
        Level = LogLevel.Information,
        Message = "Character {CharacterId} used skill {SkillKey} on creature {CreatureId} for {Damage} damage by account {AccountId} at {CentralTime}")]
    public static partial void SkillUsed(
        ILogger logger,
        Guid characterId,
        string skillKey,
        Guid? creatureId,
        int damage,
        Guid accountId,
        DateTimeOffset centralTime);

    [LoggerMessage(
        EventId = 2014,
        Level = LogLevel.Information,
        Message = "Account {AccountId} advanced the development world simulation by {WorldHours} hours at {CentralTime}")]
    public static partial void WorldAdvanced(
        ILogger logger,
        Guid accountId,
        int worldHours,
        DateTimeOffset centralTime);

    [LoggerMessage(
        EventId = 2015,
        Level = LogLevel.Warning,
        Message = "Account {AccountId} reset the development world simulation at {CentralTime}")]
    public static partial void WorldReset(
        ILogger logger,
        Guid accountId,
        DateTimeOffset centralTime);
}

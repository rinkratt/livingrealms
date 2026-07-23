namespace LivingRealms.DiscordBot;

public sealed class DiscordBotOptions
{
    public const string SectionName = "DiscordBot";

    public string Token { get; init; } = string.Empty;

    public ulong GuildId { get; init; }

    public int CooldownSeconds { get; init; } = 15;
}

public sealed class OpenAiOptions
{
    public const string SectionName = "OpenAI";

    public string ApiKey { get; init; } = string.Empty;

    public string Model { get; init; } = "gpt-5.6-luna";

    public int MaxOutputTokens { get; init; } = 500;
}

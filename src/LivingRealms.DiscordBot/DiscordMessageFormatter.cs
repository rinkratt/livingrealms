namespace LivingRealms.DiscordBot;

public static class DiscordMessageFormatter
{
    public const int MaxLength = 1_900;

    public static string Fit(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length <= MaxLength)
        {
            return trimmed;
        }

        return string.Concat(trimmed.AsSpan(0, MaxLength - 1).TrimEnd(), "…");
    }
}

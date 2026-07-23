using LivingRealms.DiscordBot;

namespace LivingRealms.DiscordBot.Tests;

public sealed class DiscordMessageFormatterTests
{
    [Fact]
    public void FitPreservesShortMessages()
    {
        Assert.Equal("Hello, traveler.", DiscordMessageFormatter.Fit("  Hello, traveler.  "));
    }

    [Fact]
    public void FitTruncatesLongMessagesWithinDiscordLimit()
    {
        var result = DiscordMessageFormatter.Fit(new string('x', 2_100));

        Assert.Equal(DiscordMessageFormatter.MaxLength, result.Length);
        Assert.EndsWith("…", result, StringComparison.Ordinal);
    }
}

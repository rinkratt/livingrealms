using System.Text.Json;
using LivingRealms.DiscordBot;

namespace LivingRealms.DiscordBot.Tests;

public sealed class OpenAiResponseParserTests
{
    [Fact]
    public void ParseUsesTopLevelOutputTextWhenPresent()
    {
        using var document = JsonDocument.Parse("""{"output_text":"  Welcome, traveler.  "}""");

        var result = OpenAiResponseParser.Parse(document.RootElement);

        Assert.Equal("Welcome, traveler.", result);
    }

    [Fact]
    public void ParseCombinesNestedOutputTextParts()
    {
        using var document = JsonDocument.Parse("""
            {
              "output": [
                {
                  "type": "message",
                  "content": [
                    { "type": "output_text", "text": "First" },
                    { "type": "output_text", "text": "Second" }
                  ]
                }
              ]
            }
            """);

        var result = OpenAiResponseParser.Parse(document.RootElement);

        Assert.Equal("First\nSecond", result);
    }

    [Fact]
    public void ParseRejectsResponsesWithoutText()
    {
        using var document = JsonDocument.Parse("""{"output":[]}""");

        var exception = Assert.Throws<InvalidOperationException>(() => OpenAiResponseParser.Parse(document.RootElement));

        Assert.Contains("did not contain text output", exception.Message, StringComparison.Ordinal);
    }
}

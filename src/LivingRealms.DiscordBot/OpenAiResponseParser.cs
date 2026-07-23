using System.Text.Json;

namespace LivingRealms.DiscordBot;

public static class OpenAiResponseParser
{
    public static string Parse(JsonElement root)
    {
        if (root.TryGetProperty("output_text", out var outputText) &&
            outputText.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(outputText.GetString()))
        {
            return outputText.GetString()!.Trim();
        }

        if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("The OpenAI response did not contain text output.");
        }

        var parts = new List<string>();

        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var part in content.EnumerateArray())
            {
                if (part.TryGetProperty("type", out var type) &&
                    type.GetString() == "output_text" &&
                    part.TryGetProperty("text", out var text) &&
                    text.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(text.GetString()))
                {
                    parts.Add(text.GetString()!.Trim());
                }
            }
        }

        if (parts.Count == 0)
        {
            throw new InvalidOperationException("The OpenAI response did not contain text output.");
        }

        return string.Join("\n", parts);
    }
}

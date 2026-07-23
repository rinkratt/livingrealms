using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace LivingRealms.DiscordBot;

public sealed partial class OpenAiResponsesClient(
    HttpClient httpClient,
    IOptions<OpenAiOptions> options,
    ILogger<OpenAiResponsesClient> logger)
{
    private readonly OpenAiOptions _options = options.Value;

    public async Task<string> AskAsync(string question, CancellationToken cancellationToken)
    {
        ValidateConfiguration();

        var payload = new
        {
            model = _options.Model,
            instructions = LivingRealmsKnowledge.Instructions,
            input = question,
            max_output_tokens = _options.MaxOutputTokens,
            store = false
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/responses")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var requestId = response.Headers.TryGetValues("x-request-id", out var values)
                ? values.FirstOrDefault()
                : null;

            LogOpenAiFailure(logger, response.StatusCode, requestId ?? "unavailable");
            throw new OpenAiRequestException(response.StatusCode, requestId);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return OpenAiResponseParser.Parse(document.RootElement);
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("OpenAI:ApiKey is required.");
        }

        if (string.IsNullOrWhiteSpace(_options.Model))
        {
            throw new InvalidOperationException("OpenAI:Model is required.");
        }

        if (_options.MaxOutputTokens is < 64 or > 4_000)
        {
            throw new InvalidOperationException("OpenAI:MaxOutputTokens must be between 64 and 4000.");
        }
    }

    [LoggerMessage(
        EventId = 2104,
        Level = LogLevel.Error,
        Message = "OpenAI request failed with HTTP {StatusCode}; request id {RequestId}")]
    private static partial void LogOpenAiFailure(ILogger logger, HttpStatusCode statusCode, string requestId);
}

public sealed class OpenAiRequestException(HttpStatusCode statusCode, string? requestId)
    : Exception($"OpenAI request failed with HTTP {(int)statusCode}.")
{
    public HttpStatusCode StatusCode { get; } = statusCode;

    public string? RequestId { get; } = requestId;
}

using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LivingRealms.Api.Tests;

public sealed class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task LivenessEndpointDoesNotRequireDatabase()
    {
        var response = await _client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.Equal("Healthy", payload?.Status);
    }

    [Fact]
    public async Task ApiRootIdentifiesPhaseSevenFirstRaidSlice()
    {
        var response = await _client.GetAsync("/api");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Living Realms Game API", body, StringComparison.Ordinal);
        Assert.Contains("\"phase\":8", body, StringComparison.Ordinal);
        Assert.Contains("settlement-development-ready", body, StringComparison.Ordinal);
    }

    private sealed record HealthResponse(string Status);
}

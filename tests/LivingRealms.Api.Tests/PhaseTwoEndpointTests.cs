using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LivingRealms.Api.Security;
using LivingRealms.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace LivingRealms.Api.Tests;

public sealed class PhaseTwoEndpointTests : IClassFixture<PhaseTwoWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly PhaseTwoWebApplicationFactory _factory;

    public PhaseTwoEndpointTests(PhaseTwoWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RegistrationCreatesAldenAndElaraAndStoresOnlyTokenHash()
    {
        using var client = _factory.CreateClient();
        var registration = await RegisterAsync(client, UniqueEmail());

        Assert.Equal(["Alden", "Elara"], registration.Characters.Select(x => x.Name).ToArray());
        Assert.Contains(registration.Characters, x => x.Name == "Alden" && x.Archetype == "Vanguard");
        Assert.Contains(registration.Characters, x => x.Name == "Elara" && x.Archetype == "Ranger");
        Assert.All(registration.Characters, x => Assert.Equal("Stonehaven Valley", x.Region));

        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
        var session = Assert.Single(database.PlayerSessions.Where(x => x.AccountId == registration.Account.Id));
        Assert.NotEqual(registration.Token, session.TokenHash);
        Assert.Equal(SessionToken.Hash(registration.Token), session.TokenHash);
    }

    [Fact]
    public async Task LoginRejectsWrongPasswordAndAcceptsCorrectPassword()
    {
        using var client = _factory.CreateClient();
        var email = UniqueEmail();
        _ = await RegisterAsync(client, email);

        var rejected = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new Credentials(email, "WrongPassword9!"));
        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);

        var accepted = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new Credentials(email, TestPassword));
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var authentication = await ReadAuthenticationAsync(accepted);
        Assert.False(string.IsNullOrWhiteSpace(authentication.Token));
    }

    [Fact]
    public async Task SelectedCharacterPositionIsSavedAndRestored()
    {
        using var client = _factory.CreateClient();
        var registration = await RegisterAsync(client, UniqueEmail());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registration.Token);
        var alden = Assert.Single(registration.Characters, x => x.Name == "Alden");

        var selected = await client.PostAsync($"/api/v1/characters/{alden.Id:D}/select", null);
        Assert.Equal(HttpStatusCode.OK, selected.StatusCode);

        var saved = await client.PutAsJsonAsync(
            $"/api/v1/characters/{alden.Id:D}/position",
            new Position(125.5f, 7.25f, -48.75f));
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        var restored = await client.GetFromJsonAsync<CharacterResponse>(
            "/api/v1/characters/current",
            JsonOptions);
        Assert.NotNull(restored);
        Assert.Equal(125.5f, restored.Position.X);
        Assert.Equal(7.25f, restored.Position.Y);
        Assert.Equal(-48.75f, restored.Position.Z);
    }

    [Fact]
    public async Task CharacterEndpointsRequireAuthentication()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/characters");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<AuthenticationResponse> RegisterAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/accounts/register",
            new Credentials(email, TestPassword));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAuthenticationAsync(response);
    }

    private static async Task<AuthenticationResponse> ReadAuthenticationAsync(HttpResponseMessage response)
    {
        var authentication = await response.Content.ReadFromJsonAsync<AuthenticationResponse>(JsonOptions);
        return Assert.IsType<AuthenticationResponse>(authentication);
    }

    private static string UniqueEmail() => $"player-{Guid.NewGuid():N}@living-realms.test";

    private const string TestPassword = "Stonehaven42!";

    private sealed record Credentials(string Email, string Password);
    private sealed record Position(float X, float Y, float Z);
    private sealed record AuthenticationResponse(
        string Token,
        DateTimeOffset ExpiresAt,
        AccountResponse Account,
        CharacterResponse[] Characters);
    private sealed record AccountResponse(Guid Id, string Email);
    private sealed record CharacterResponse(
        Guid Id,
        string Name,
        string Archetype,
        int Level,
        long Experience,
        int Health,
        int MaximumHealth,
        string Region,
        Position Position,
        DateTimeOffset UpdatedAt);
}

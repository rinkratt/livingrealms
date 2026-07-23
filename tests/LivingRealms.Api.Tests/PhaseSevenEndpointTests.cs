using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LivingRealms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LivingRealms.Api.Tests;

public sealed class PhaseSevenEndpointTests : IClassFixture<PhaseTwoWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly PhaseTwoWebApplicationFactory _factory;

    public PhaseSevenEndpointTests(PhaseTwoWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ResidentRosterRequiresAnAuthenticatedSelectedCharacter()
    {
        using var client = _factory.CreateClient();
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/v1/regions/stonehaven-valley/residents")).StatusCode);

        var registration = await RegisterAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registration.Token);
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await client.GetAsync("/api/v1/regions/stonehaven-valley/residents")).StatusCode);
    }

    [Fact]
    public async Task StonehavenReturnsPersistentNamedResidentsWithRaidReadyState()
    {
        using var client = _factory.CreateClient();
        var registration = await RegisterAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registration.Token);
        await SelectAldenAsync(client, registration);

        var residents = await client.GetFromJsonAsync<ResidentResponse[]>(
            "/api/v1/regions/stonehaven-valley/residents",
            JsonOptions);

        Assert.NotNull(residents);
        Assert.Equal(9, residents.Length);
        Assert.Equal(9, residents.Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(8, residents.Count(x => x.Status == "Active"));
        Assert.Contains(residents, x => x.Name == "Mara" && x.Status == "Missing");
        Assert.Contains(residents, x => x.Name == "Captain Rowan" && x.Role == "Guard Captain" && x.CanFight);
        Assert.Contains(residents, x => x.Name == "Elowen" && x.Role == "Healer" && !x.CanFight);
        Assert.Contains(residents, x => x.Name == "Nessa" && x.Role == "Lumberjack" && !x.CanFight);
        Assert.Contains(residents, x => x.Name == "Dain" && x.Role == "Quarry Worker" && !x.CanFight);
        Assert.All(residents.Where(x => x.Status == "Active"), resident =>
        {
            Assert.Equal("Active", resident.Status);
            Assert.Equal(resident.MaximumHealth, resident.Health);
            Assert.False(string.IsNullOrWhiteSpace(resident.Dialogue));
            Assert.NotEmpty(resident.Skills);
            Assert.True(resident.WorldDay >= 1);
            Assert.InRange(resident.WorldHour, 0, 23);
        });

        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
        var settlementPopulation = await database.Settlements
            .Where(x => x.Id == LivingRealmsDbContext.StonehavenVillageId)
            .Select(x => x.Population)
            .SingleAsync();
        Assert.Equal(settlementPopulation, await database.SettlementResidents.CountAsync(x =>
            x.Status == LivingRealms.Domain.Entities.ResidentStatus.Active ||
            x.Status == LivingRealms.Domain.Entities.ResidentStatus.Injured));
    }

    [Fact]
    public async Task ResidentActivitiesFollowTheServerWorldClock()
    {
        using var client = _factory.CreateClient();
        var registration = await RegisterAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registration.Token);
        await SelectAldenAsync(client, registration);

        await SetSimulatedHoursAsync(8);
        var morning = await client.GetFromJsonAsync<ResidentResponse[]>(
            "/api/v1/regions/stonehaven-valley/residents",
            JsonOptions);
        Assert.NotNull(morning);
        Assert.Equal("Patrolling Stonehaven", Assert.Single(morning, x => x.Name == "Captain Rowan").Activity);
        Assert.Equal("Working as blacksmith", Assert.Single(morning, x => x.Name == "Brann").Activity);

        await SetSimulatedHoursAsync(23);
        var night = await client.GetFromJsonAsync<ResidentResponse[]>(
            "/api/v1/regions/stonehaven-valley/residents",
            JsonOptions);
        Assert.NotNull(night);
        Assert.Equal("Guarding the gate", Assert.Single(night, x => x.Name == "Mira").Activity);
        Assert.Equal("Missing", Assert.Single(night, x => x.Name == "Mara").Activity);
        Assert.Equal("Resting at home", Assert.Single(night, x => x.Name == "Brann").Activity);
    }

    private async Task SetSimulatedHoursAsync(long hours)
    {
        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
        var faction = await database.Factions.SingleAsync(x => x.Id == LivingRealmsDbContext.DarkwoodClanId);
        faction.SimulatedHours = hours;
        await database.SaveChangesAsync();
    }

    private static async Task<AuthenticationResponse> RegisterAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/accounts/register",
            new Credentials($"phase7-{Guid.NewGuid():N}@living-realms.test", "Stonehaven42!"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var registration = await response.Content.ReadFromJsonAsync<AuthenticationResponse>(JsonOptions);
        return Assert.IsType<AuthenticationResponse>(registration);
    }

    private static async Task SelectAldenAsync(HttpClient client, AuthenticationResponse registration)
    {
        var alden = Assert.Single(registration.Characters, x => x.Name == "Alden");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/v1/characters/{alden.Id:D}/select", null)).StatusCode);
    }

    private sealed record Credentials(string Email, string Password);
    private sealed record AuthenticationResponse(string Token, CharacterResponse[] Characters);
    private sealed record CharacterResponse(Guid Id, string Name);
    private sealed record PositionResponse(float X, float Y, float Z);
    private sealed record ResidentResponse(
        Guid Id,
        string Name,
        string Role,
        int Health,
        int MaximumHealth,
        string Status,
        bool CanFight,
        string[] Skills,
        string Activity,
        PositionResponse Position,
        PositionResponse HomePosition,
        PositionResponse WorkPosition,
        PositionResponse SafePosition,
        string Dialogue,
        int WorldHour,
        int WorldDay,
        DateTimeOffset ServerTimeCentral);
}

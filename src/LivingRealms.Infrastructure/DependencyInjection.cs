using LivingRealms.Infrastructure.Persistence;
using LivingRealms.Infrastructure.Simulation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LivingRealms.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddLivingRealmsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("GameDatabase")
            ?? throw new InvalidOperationException("ConnectionStrings:GameDatabase is required.");
        services.AddDbContext<LivingRealmsDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(LivingRealmsDbContext).Assembly.FullName)));
        services.AddScoped<WorldSimulationService>();
        services.AddScoped<RaidSimulationService>();
        services.AddScoped<WorldPopulationService>();
        return services;
    }
}

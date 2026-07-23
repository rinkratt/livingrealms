using LivingRealms.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace LivingRealms.Api.Tests;

public sealed class PhaseTwoWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"living-realms-tests-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<LivingRealmsDbContext>>();
            services.RemoveAll<LivingRealmsDbContext>();
            services.AddDbContext<LivingRealmsDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>().Database.EnsureCreated();
        return host;
    }
}

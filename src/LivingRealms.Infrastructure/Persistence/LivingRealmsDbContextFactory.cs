using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LivingRealms.Infrastructure.Persistence;

public sealed class LivingRealmsDbContextFactory : IDesignTimeDbContextFactory<LivingRealmsDbContext>
{
    public LivingRealmsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("LIVING_REALMS_DB")
            ?? "Host=127.0.0.1;Port=5432;Database=living_realms_dev;Username=living_realms;Password=development_only";
        var options = new DbContextOptionsBuilder<LivingRealmsDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(LivingRealmsDbContext).Assembly.FullName))
            .Options;
        return new LivingRealmsDbContext(options);
    }
}

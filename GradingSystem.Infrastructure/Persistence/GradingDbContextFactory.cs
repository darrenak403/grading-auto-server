using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace GradingSystem.Infrastructure.Persistence;

public class GradingDbContextFactory : IDesignTimeDbContextFactory<GradingDbContext>
{
    public GradingDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "Development";

        var startupProjectPath = ResolveStartupProjectPath();
        var configuration = new ConfigurationBuilder()
            .SetBasePath(startupProjectPath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .Build();

        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? configuration.GetConnectionString("Postgres")
            ?? "Host=localhost;Port=5439;Database=grading_system;Username=postgres;Password=grading_pass";

        var opts = new DbContextOptionsBuilder<GradingDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new GradingDbContext(opts);
    }

    private static string ResolveStartupProjectPath()
    {
        var cwd = Directory.GetCurrentDirectory();
        var candidates = new[]
        {
            Path.Combine(cwd, "GradingSystem.Api"),
            Path.Combine(cwd, "..", "GradingSystem.Api"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "GradingSystem.Api")
        };

        return candidates
            .Select(Path.GetFullPath)
            .FirstOrDefault(Directory.Exists)
            ?? cwd;
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GradingSystem.Infrastructure.Persistence;

public class GradingDbContextFactory : IDesignTimeDbContextFactory<GradingDbContext>
{
    public GradingDbContext CreateDbContext(string[] args)
    {
        var opts = new DbContextOptionsBuilder<GradingDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=grading_system;Username=postgres;Password=grading_pass")
            .Options;
        return new GradingDbContext(opts);
    }
}

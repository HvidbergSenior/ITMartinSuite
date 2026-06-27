using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ITMartin.FamilieOverblik.Infrastructure;

public class FamilieOverblikContextFactory
    : IDesignTimeDbContextFactory<FamilieOverblikDbContext>
{
    public FamilieOverblikDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<FamilieOverblikDbContext>()
            .UseSqlite("Data Source=familie.db")
            .Options;

        return new FamilieOverblikDbContext(options);
    }
}

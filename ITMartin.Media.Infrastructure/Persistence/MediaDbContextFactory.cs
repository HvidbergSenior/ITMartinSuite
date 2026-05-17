using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ITMartin.Media.Infrastructure.Persistence;

public sealed class MediaDbContextFactory
    : IDesignTimeDbContextFactory<MediaDbContext>
{
    public MediaDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder =
            new DbContextOptionsBuilder<MediaDbContext>();

        optionsBuilder.UseSqlite(
            "Data Source=C:\\ITMartin\\Data\\media.db");

        return new MediaDbContext(optionsBuilder.Options);
    }
}
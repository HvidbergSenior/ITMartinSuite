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

        const string connectionString =
            "Data Source=C:\\ITMartin\\Data\\media.db";

        optionsBuilder.UseSqlite(
            connectionString,
            builder =>
            {
                builder.MigrationsAssembly(
                    typeof(MediaDbContext).Assembly.FullName);
            });

        return new MediaDbContext(optionsBuilder.Options);
    }
}
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ITMartin.Magic.Infrastructure.Persistence;

public sealed class MagicDbContextFactory
    : IDesignTimeDbContextFactory<MagicDbContext>
{
    public MagicDbContext CreateDbContext(
        string[] args)
    {
        var builder =
            new DbContextOptionsBuilder<MagicDbContext>();

        builder.UseSqlite(
            "Data Source=magic.db");

        return new MagicDbContext(
            builder.Options);
    }
}
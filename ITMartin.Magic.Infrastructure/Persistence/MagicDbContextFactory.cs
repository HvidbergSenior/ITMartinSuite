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

        builder.UseNpgsql(
            "Host=localhost;Port=5432;Database=magic;Username=postgres;Password=magic");

        return new MagicDbContext(
            builder.Options);
    }
}
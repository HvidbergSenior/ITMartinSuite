using ITMartin.Magic.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartin.Magic.Infrastructure.Persistence;

public sealed class MagicDbContext
    : DbContext
{
    public MagicDbContext(
        DbContextOptions<MagicDbContext> options)
        : base(options)
    {
    }

    public DbSet<MagicSetKnowledge> Sets =>
        Set<MagicSetKnowledge>();

    public DbSet<MagicCard> Cards =>
        Set<MagicCard>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(MagicDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
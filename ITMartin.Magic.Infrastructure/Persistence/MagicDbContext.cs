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

    public DbSet<MagicSetKnowledge>
        Sets
        => Set<MagicSetKnowledge>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<MagicSetKnowledge>()
            .HasKey(x => x.SetCode);
    }
}
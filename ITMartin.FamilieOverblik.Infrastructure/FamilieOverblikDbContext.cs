using ITMartin.FamilieOverblik.Domain;
using Microsoft.EntityFrameworkCore;

namespace ITMartin.FamilieOverblik.Infrastructure;

public class FamilieOverblikDbContext : DbContext
{
    public FamilieOverblikDbContext(
        DbContextOptions<FamilieOverblikDbContext> options)
        : base(options)
    {
    }

    public DbSet<FamilyTask> Tasks => Set<FamilyTask>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<FamilyTask>()
            .HasIndex(x => x.CreatedAt);
    }
}

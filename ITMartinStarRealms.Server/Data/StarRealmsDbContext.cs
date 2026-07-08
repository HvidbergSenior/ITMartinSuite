using ITMartinStarRealms.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinStarRealms.Server.Data;

public sealed class StarRealmsDbContext : DbContext
{
    public StarRealmsDbContext(DbContextOptions<StarRealmsDbContext> options) : base(options) { }

    public DbSet<GameSession> Sessions => Set<GameSession>();
    public DbSet<GamePlayer> Players   => Set<GamePlayer>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<GameSession>()
            .HasIndex(s => s.Code)
            .IsUnique();

        model.Entity<GameSession>()
            .HasMany(s => s.Players)
            .WithOne()
            .HasForeignKey(p => p.SessionId);
    }
}

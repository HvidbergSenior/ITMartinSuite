using ITMartinStarRealms.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinStarRealms.Server.Data;

public sealed class StarRealmsDbContext : DbContext
{
    public StarRealmsDbContext(DbContextOptions<StarRealmsDbContext> options) : base(options) { }

    public DbSet<GameSession> Sessions => Set<GameSession>();
    public DbSet<GamePlayer> Players   => Set<GamePlayer>();
    public DbSet<GameRuleset> Rulesets => Set<GameRuleset>();
    public DbSet<PlayerProfile> Profiles => Set<PlayerProfile>();
    public DbSet<GameResult> Results => Set<GameResult>();
    public DbSet<GameResultPlayer> ResultPlayers => Set<GameResultPlayer>();
    public DbSet<GameEvent> Events => Set<GameEvent>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<GameSession>()
            .HasIndex(s => s.Code)
            .IsUnique();

        model.Entity<GameSession>()
            .HasMany(s => s.Players)
            .WithOne()
            .HasForeignKey(p => p.SessionId);

        model.Entity<PlayerProfile>()
            .HasIndex(p => p.DeviceToken)
            .IsUnique();

        model.Entity<GameResult>()
            .HasMany(r => r.Players)
            .WithOne()
            .HasForeignKey(p => p.GameResultId);
    }
}

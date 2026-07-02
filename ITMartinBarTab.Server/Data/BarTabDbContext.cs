using ITMartinBarTab.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinBarTab.Server.Data;

public sealed class BarTabDbContext : DbContext
{
    public BarTabDbContext(DbContextOptions<BarTabDbContext> options) : base(options) { }

    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<DrinkEntry> Drinks => Set<DrinkEntry>();
    public DbSet<DrinkShare> DrinkShares => Set<DrinkShare>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Session>()
            .HasIndex(s => s.Code)
            .IsUnique();

        model.Entity<DrinkShare>()
            .HasIndex(s => new { s.DrinkEntryId, s.ParticipantId })
            .IsUnique();

        model.Entity<DrinkEntry>()
            .Property(d => d.Price)
            .HasColumnType("TEXT");
    }
}

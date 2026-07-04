using ITMartinAuction.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinAuction.Server.Data;

public sealed class AuctionDbContext : DbContext
{
    public AuctionDbContext(DbContextOptions<AuctionDbContext> options) : base(options) { }

    public DbSet<AuctionSession> Sessions     => Set<AuctionSession>();
    public DbSet<AuctionItem>   Items         => Set<AuctionItem>();
    public DbSet<Bidder>        Bidders       => Set<Bidder>();
    public DbSet<Bid>           Bids          => Set<Bid>();
    public DbSet<ChatMessage>   ChatMessages  => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<AuctionSession>()
            .HasIndex(s => s.Code)
            .IsUnique();

        model.Entity<AuctionItem>()
            .Property(i => i.StartingPrice)
            .HasColumnType("TEXT");

        model.Entity<AuctionItem>()
            .Property(i => i.WinningBid)
            .HasColumnType("TEXT");

        model.Entity<Bid>()
            .Property(b => b.Amount)
            .HasColumnType("TEXT");
    }
}

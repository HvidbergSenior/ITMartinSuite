using ITMartinLager.Domain;
using Microsoft.EntityFrameworkCore;

namespace ITMartinLager.Infrastructure.Persistence;

public sealed class LagerDb(DbContextOptions<LagerDb> options) : DbContext(options)
{
    public DbSet<Box> Boxes => Set<Box>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<Photo> Photos => Set<Photo>();
    public DbSet<AiDay> AiDays => Set<AiDay>();
    public DbSet<BookCheck> BookChecks => Set<BookCheck>();
    public DbSet<PriceCheck> PriceChecks => Set<PriceCheck>();
    public DbSet<PriceFind> PriceFinds => Set<PriceFind>();
    public DbSet<GoldEntry> GoldEntries => Set<GoldEntry>();
    public DbSet<SaleBooking> SaleBookings => Set<SaleBooking>();
    public DbSet<KioskPrice> KioskPrices => Set<KioskPrice>();
    public DbSet<BoxShot> BoxShots => Set<BoxShot>();
    public DbSet<BoxScan> BoxScans => Set<BoxScan>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Box>().HasIndex(x => x.Code).IsUnique();
        b.Entity<Item>().HasIndex(x => x.Search);
        b.Entity<Item>().HasIndex(x => x.Kind);
        b.Entity<Item>().HasIndex(x => x.BoxId);
        b.Entity<Item>().Property(x => x.Price).HasConversion<double?>();
        b.Entity<Item>().Property(x => x.SoldPrice).HasConversion<double?>();
        b.Entity<Item>().Property(x => x.PriceHint).HasConversion<double?>();
        b.Entity<Item>().Property(x => x.SimilarHint).HasConversion<double?>();
        b.Entity<Item>().HasIndex(x => x.MagicRef);
        b.Entity<Item>().HasIndex(x => x.AuctionItemId);
        b.Entity<AiDay>().HasKey(x => x.Day);
        b.Entity<BookCheck>().HasIndex(x => x.Search);
        b.Entity<BookCheck>().HasIndex(x => x.CheckedAt);
        b.Entity<Item>().HasIndex(x => x.PricesAt);
        b.Entity<PriceCheck>().HasIndex(x => x.ItemId);
        b.Entity<PriceFind>().HasIndex(x => x.ItemId);
        b.Entity<GoldEntry>().HasIndex(x => x.Category);
        b.Entity<SaleBooking>().HasIndex(x => x.Ref).IsUnique();
        b.Entity<BoxShot>().HasIndex(x => x.VareinfoId).IsUnique();
        b.Entity<BoxShot>().HasIndex(x => x.BoxCode);
        b.Entity<BoxScan>().HasIndex(x => x.VareinfoId).IsUnique();
        b.Entity<BoxScan>().HasIndex(x => x.BoxCode);
    }
}

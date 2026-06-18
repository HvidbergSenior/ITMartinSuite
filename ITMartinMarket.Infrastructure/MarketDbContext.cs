using ITMartinMarket.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinMarket.Infrastructure;

public sealed class MarketDbContext(DbContextOptions<MarketDbContext> options) : DbContext(options)
{
    public DbSet<SaleItem> Items => Set<SaleItem>();
    public DbSet<Bid> Bids => Set<Bid>();
    public DbSet<ItemMessage> Messages => Set<ItemMessage>();
}

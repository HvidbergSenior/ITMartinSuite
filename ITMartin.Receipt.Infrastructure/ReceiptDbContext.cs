using ITMartin.Receipt.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartin.Receipt.Infrastructure;

public class ReceiptDbContext : DbContext
{
    public ReceiptDbContext(DbContextOptions<ReceiptDbContext> options)
        : base(options) { }

    public DbSet<ReceiptTransaction> Transactions => Set<ReceiptTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ReceiptTransaction>()
            .OwnsMany(x => x.Items, owned =>
            {
                owned.Property<int>("Id").ValueGeneratedOnAdd();
                owned.HasKey("Id");
                owned.WithOwner().HasForeignKey("TransactionId");
            });
    }
}

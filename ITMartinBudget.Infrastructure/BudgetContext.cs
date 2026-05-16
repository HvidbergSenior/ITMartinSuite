using ITMartinBudget.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinBudget.Infrastructure;

public class BudgetDbContext : DbContext
{
    public BudgetDbContext(
        DbContextOptions<BudgetDbContext> options)
        : base(options)
    {
    }

    public DbSet<BankTransaction> Transactions =>
        Set<BankTransaction>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // =====================================
        // UNIQUE TRANSACTION
        // =====================================

        modelBuilder.Entity<BankTransaction>()
            .HasIndex(x => new
            {
                x.Date,
                x.Amount,
                x.NormalizedDescription
            })
            .IsUnique();

        // =====================================
        // TRANSACTION CONFIG
        // =====================================

        modelBuilder.Entity<BankTransaction>()
            .Property(x => x.Description)
            .HasMaxLength(1000);

        modelBuilder.Entity<BankTransaction>()
            .Property(x => x.NormalizedDescription)
            .HasMaxLength(1000);

        modelBuilder.Entity<BankTransaction>()
            .Property(x => x.Title)
            .HasMaxLength(300);

        // =====================================
        // INDEXES
        // =====================================

        modelBuilder.Entity<BankTransaction>()
            .HasIndex(x => x.Category);

        modelBuilder.Entity<BankTransaction>()
            .HasIndex(x => x.BudgetGroup);

        modelBuilder.Entity<BankTransaction>()
            .HasIndex(x => x.TransactionType);

        modelBuilder.Entity<BankTransaction>()
            .HasIndex(x => x.ImportedAt);

        modelBuilder.Entity<BankTransaction>()
            .HasIndex(x => x.IsRecurring);
    }
}
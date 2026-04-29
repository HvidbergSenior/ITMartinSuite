using ITMartinBudget.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinBudget.Infrastructure;

public class BudgetDbContext : DbContext
{
    public BudgetDbContext(DbContextOptions<BudgetDbContext> options)
        : base(options)
    {
    }

    public DbSet<BankTransaction> Transactions => Set<BankTransaction>();
    public DbSet<CategoryRule> CategoryRules => Set<CategoryRule>();
    public DbSet<UnknownTransaction> UnknownTransactions => Set<UnknownTransaction>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BankTransaction>()
            .HasIndex(x => new { x.Date, x.Amount, x.Description })
            .IsUnique();
    }
}
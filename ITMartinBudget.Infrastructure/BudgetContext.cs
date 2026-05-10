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

    public DbSet<CategoryRule> CategoryRules =>
        Set<CategoryRule>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // =====================================
        // TRANSACTION UNIQUE INDEX
        // =====================================

        modelBuilder.Entity<BankTransaction>()
            .HasIndex(x => new
            {
                x.Date,
                x.Amount,
                x.Description
            })
            .IsUnique();

        // =====================================
        // TRANSACTION -> MATCHED RULE
        // =====================================

        modelBuilder.Entity<BankTransaction>()
            .HasOne(x => x.MatchedRule)
            .WithMany()
            .HasForeignKey(x => x.MatchedRuleId)
            .OnDelete(DeleteBehavior.SetNull);

        // =====================================
        // CATEGORY RULE CONFIG
        // =====================================

        modelBuilder.Entity<CategoryRule>()
            .Property(x => x.Pattern)
            .HasMaxLength(300);

        modelBuilder.Entity<CategoryRule>()
            .HasIndex(x => x.Pattern);

        modelBuilder.Entity<CategoryRule>()
            .HasIndex(x => x.Priority);

        modelBuilder.Entity<CategoryRule>()
            .HasIndex(x => x.IsActive);

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
            .HasIndex(x => x.Category);

        modelBuilder.Entity<BankTransaction>()
            .HasIndex(x => x.TransactionType);

        modelBuilder.Entity<BankTransaction>()
            .HasIndex(x => x.NeedsReview);

        modelBuilder.Entity<BankTransaction>()
            .HasIndex(x => x.ImportedAt);
    }
}
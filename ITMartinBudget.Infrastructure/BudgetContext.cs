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

    public DbSet<PlannedTransaction> PlannedTransactions =>
        Set<PlannedTransaction>();

    public DbSet<CategoryRule> CategoryRules =>
        Set<CategoryRule>();

    public DbSet<LedgerConfig> LedgerConfigs =>
        Set<LedgerConfig>();

    public DbSet<TransactionInvestigation> TransactionInvestigations =>
        Set<TransactionInvestigation>();

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
                x.LedgerId,
                x.Date,
                x.Amount,
                x.NormalizedDescription
            });

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
            .HasIndex(x => x.LedgerId);

        modelBuilder.Entity<BankTransaction>()
            .Property(x => x.LedgerId)
            .HasMaxLength(100);

        // =====================================
        // CATEGORY RULES
        // =====================================

        modelBuilder.Entity<CategoryRule>()
            .HasIndex(x => new { x.LedgerId, x.Pattern })
            .IsUnique();

        modelBuilder.Entity<CategoryRule>()
            .Property(x => x.LedgerId)
            .HasMaxLength(100);

        modelBuilder.Entity<CategoryRule>()
            .Property(x => x.Pattern)
            .HasMaxLength(1000);

        modelBuilder.Entity<CategoryRule>()
            .Property(x => x.CategoryName)
            .HasMaxLength(200);

        // =====================================
        // LEDGER CONFIG
        // =====================================

        modelBuilder.Entity<LedgerConfig>()
            .HasKey(x => x.LedgerId);

        modelBuilder.Entity<LedgerConfig>()
            .Property(x => x.LedgerId)
            .HasMaxLength(100);

        // =====================================
        // TRANSACTION INVESTIGATION
        // =====================================

        modelBuilder.Entity<TransactionInvestigation>()
            .HasKey(x => new { x.LedgerId, x.Pattern });

        modelBuilder.Entity<TransactionInvestigation>()
            .Property(x => x.LedgerId)
            .HasMaxLength(100);

        modelBuilder.Entity<TransactionInvestigation>()
            .Property(x => x.Pattern)
            .HasMaxLength(1000);
    }
}
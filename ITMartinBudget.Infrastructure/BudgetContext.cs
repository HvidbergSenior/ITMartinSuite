using ITMartinBudget.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinBudgetInfrastructure;

public class BudgetDbContext : DbContext
{
    public DbSet<BankTransaction> Transactions => Set<BankTransaction>();

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite("Data Source=budget.db");
}
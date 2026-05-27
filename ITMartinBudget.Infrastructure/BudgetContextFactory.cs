using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ITMartinBudget.Infrastructure;

public class BudgetDbContextFactory
    : IDesignTimeDbContextFactory<BudgetDbContext>
{
    public BudgetDbContext CreateDbContext(
        string[] args)
    {
        var optionsBuilder =
            new DbContextOptionsBuilder<BudgetDbContext>();

        optionsBuilder.UseSqlite(
            "Data Source=C:\\ITMartin\\Data\\budget.db");

        return new BudgetDbContext(
            optionsBuilder.Options);
    }
}
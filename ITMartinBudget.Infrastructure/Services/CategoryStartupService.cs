using ITMartinBudget.Domain.Entities;
using ITMartinBudget.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;
namespace ITMartinBudget.Infrastructure.Services;

public class CategoryRuleStartupService
{
    private readonly BudgetDbContext _db;

    public CategoryRuleStartupService(
        BudgetDbContext db)
    {
        _db = db;
    }

    public async Task SeedAsync()
    {
        var existingPatterns =
            (await _db.CategoryRules
                .Select(x => x.Pattern)
                .ToListAsync())
            .ToHashSet();
        
        var missingRules =
            CategoryRuleSeeder
                .Get()
                .Where(x =>
                    !existingPatterns.Contains(
                        x.Pattern))
                .ToList();

        if (!missingRules.Any())
        {
            return;
        }

        await _db.CategoryRules
            .AddRangeAsync(missingRules);

        await _db.SaveChangesAsync();
    }
}
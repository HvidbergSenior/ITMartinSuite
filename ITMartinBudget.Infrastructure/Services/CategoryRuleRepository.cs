using ITMartinBudget.Application.Interfaces;
using ITMartinBudget.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinBudget.Infrastructure.Services;

public class CategoryRuleRepository : ICategoryRuleRepository
{
    private readonly BudgetDbContext _db;

    public CategoryRuleRepository(BudgetDbContext db)
    {
        _db = db;
    }

    public Task<List<CategoryRule>> GetAllAsync()
    {
        return _db.CategoryRules.ToListAsync();
    }
}
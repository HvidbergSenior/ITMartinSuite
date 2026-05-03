using ITMartinBudget.Domain.Entities;

namespace ITMartinBudget.Application.Interfaces;

public interface ICategoryRuleRepository
{
    Task<List<CategoryRule>> GetAllAsync();
}
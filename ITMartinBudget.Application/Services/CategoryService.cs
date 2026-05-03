using ITMartinBudget.Application.Interfaces;
using ITMartinBudget.Domain.Entities;

namespace ITMartinBudget.Application.Services;

public class CategoryService : ICategoryService
{
    private readonly ICategoryRuleRepository _repo;
    private CategoryEngine? _engine;

    public CategoryService(ICategoryRuleRepository repo)
    {
        _repo = repo;
    }

    public async Task<SubCategory> DetectAsync(string groupingKey, BankTransaction tx)
    {
        if (tx.IsMobilePay)
        {
            return tx.Amount > 0
                ? SubCategory.MobilePayFraAndre
                : SubCategory.MobilePayTilAndre;
        }

        _engine ??= new CategoryEngine(await _repo.GetAllAsync());

        return _engine.Detect(groupingKey);
    }
}
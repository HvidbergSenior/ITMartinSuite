using ITMartinBudget.Domain.Entities;

namespace ITMartinBudget.Application.Services;

public class CategoryEngine
{
    private readonly List<CategoryRule> _rules;

    public CategoryEngine(List<CategoryRule> rules)
    {
        _rules = rules
            .Where(r => r.IsActive)
            .OrderByDescending(r => r.Priority)
            .ToList();
    }

    public SubCategory Detect(string kategori, string tekst)
    {
        var combined = $"{kategori} {tekst}".ToLowerInvariant();

        foreach (var rule in _rules)
        {
            if (combined.Contains(rule.Keyword))
                return rule.SubCategory;
        }

        return SubCategory.Ukendt;
    }
}
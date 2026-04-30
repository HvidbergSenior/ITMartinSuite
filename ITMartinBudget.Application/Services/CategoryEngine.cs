using ITMartinBudget.Domain.Entities;

public class CategoryEngine
{
    private readonly List<CategoryRule> _rules;

    public CategoryEngine(List<CategoryRule> rules)
    {
        _rules = rules
            .Where(r => r.IsActive)
            .Select(r =>
            {
                r.Keyword = r.Keyword.ToLowerInvariant().Trim();
                return r;
            })
            .OrderByDescending(r => r.Priority)
            .ToList();
    }

    public SubCategory Detect(string text)
    {
        var normalized = text.ToLowerInvariant();

        foreach (var rule in _rules)
        {
            if (normalized.Contains(rule.Keyword))
                return rule.SubCategory;
        }

        return SubCategory.Ukendt;
    }
}
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

        Console.WriteLine($"[ENGINE INPUT] {normalized}");

        foreach (var rule in _rules)
        {
            if (normalized.Contains(rule.Keyword))
            {
                Console.WriteLine($"[MATCH] '{rule.Keyword}' => {rule.SubCategory}");
                return rule.SubCategory;
            }
        }

        Console.WriteLine("[NO MATCH]");
        return SubCategory.Ukendt;
    }
}
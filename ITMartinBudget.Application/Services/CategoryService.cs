using ITMartinBudget.Application.Interfaces;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Services;

public class CategoryService : ICategoryService
{
    public Task<Category> DetectAsync(string text)
    {
        text = text
            .ToLowerInvariant()
            .Trim();

        foreach (var rule in CategoryRules.Rules
                     .OrderByDescending(x => x.Key.Length))
        {
            if (text.Contains(rule.Key))
            {
                return Task.FromResult(rule.Value);
            }
        }

        return Task.FromResult(Category.Other);
    }
}
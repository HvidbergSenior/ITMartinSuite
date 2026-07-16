using ITMartinBudget.Application.Interfaces;
using ITMartinBudget.Domain.Entities;
using ITMartinBudget.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ITMartinBudget.Infrastructure.Services;

public sealed class CategoryRuleService : ICategoryRuleService
{
    private readonly BudgetDbContext _db;

    public CategoryRuleService(BudgetDbContext db)
    {
        _db = db;
    }

    public async Task<List<TransactionCluster>> GetClustersAsync(string ledgerId, CancellationToken cancellationToken = default)
    {
        var transactions = await _db.Transactions
            .Where(x => x.LedgerId == ledgerId)
            .ToListAsync(cancellationToken);

        var investigations = await _db.TransactionInvestigations
            .Where(x => x.LedgerId == ledgerId)
            .ToDictionaryAsync(x => x.Pattern, cancellationToken);

        return transactions
            .GroupBy(x => x.NormalizedDescription)
            .Select(g =>
            {
                investigations.TryGetValue(g.Key, out var inv);
                return new TransactionCluster(
                    g.Key,
                    g.First().Description,
                    g.Count(),
                    g.Sum(x => x.Amount),
                    g.First().Scope,
                    g.First().UserCategoryName,
                    g.Min(x => x.Date),
                    g.Max(x => x.Date),
                    g.Select(x => x.RawDetails)
                        .FirstOrDefault(d => !string.IsNullOrWhiteSpace(d) && d != g.First().Description),
                    inv?.Reasoning,
                    inv?.SuggestedScope,
                    inv?.Confidence);
            })
            .OrderBy(c => c.CurrentCategoryName is null ? 0 : 1)
            .ThenBy(c => c.Scope == TransactionScope.Unknown ? 0 : c.Scope == TransactionScope.Business ? 1 : 2)
            .ThenByDescending(c => Math.Abs(c.Sum))
            .ToList();
    }

    public async Task<List<string>> GetExistingCategoryNamesAsync(string ledgerId, CancellationToken cancellationToken = default)
    {
        return await _db.CategoryRules
            .Where(x => x.LedgerId == ledgerId)
            .Select(x => x.CategoryName)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
    }

    public async Task AssignAsync(string ledgerId, string pattern, string categoryName, TransactionScope scope, CancellationToken cancellationToken = default)
    {
        var rule = await _db.CategoryRules
            .FirstOrDefaultAsync(x => x.LedgerId == ledgerId && x.Pattern == pattern, cancellationToken);

        if (rule is null)
        {
            rule = new CategoryRule { LedgerId = ledgerId, Pattern = pattern };
            _db.CategoryRules.Add(rule);
        }

        rule.CategoryName = categoryName;
        rule.Scope = scope;

        var matching = await _db.Transactions
            .Where(x => x.LedgerId == ledgerId && x.NormalizedDescription == pattern)
            .ToListAsync(cancellationToken);

        foreach (var tx in matching)
        {
            tx.UserCategoryName = categoryName;
            tx.Scope = scope;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<LedgerCategorySummary>> GetCategorySummaryAsync(string ledgerId, CancellationToken cancellationToken = default)
    {
        var transactions = await _db.Transactions
            .Where(x => x.LedgerId == ledgerId && x.UserCategoryName != null)
            .ToListAsync(cancellationToken);

        return transactions
            .GroupBy(x => x.UserCategoryName!)
            .Select(g =>
            {
                var distinctScopes = g.Select(x => x.Scope).Distinct().ToList();
                // A category is "people" only if every underlying pattern is
                // a MobilePay-to-a-person one - a category that mixes a
                // person pattern with something else (e.g. after a manual
                // merge) isn't purely a person bucket anymore.
                var isPeople = g.All(x =>
                    x.NormalizedDescription.Contains("mobilepay") || x.NormalizedDescription.Contains("mob pay"));
                return new LedgerCategorySummary(
                    g.Key,
                    g.Count(),
                    g.Sum(x => x.Amount),
                    distinctScopes[0],
                    distinctScopes.Count > 1,
                    isPeople);
            })
            // Biggest/most-recurring categories first - easier to spot
            // consolidation candidates than a flat alphabetical list.
            .OrderByDescending(c => c.Count)
            .ToList();
    }

    public async Task MergeCategoriesAsync(string ledgerId, List<string> sourceNames, string targetName, CancellationToken cancellationToken = default)
    {
        var rules = await _db.CategoryRules
            .Where(x => x.LedgerId == ledgerId && sourceNames.Contains(x.CategoryName))
            .ToListAsync(cancellationToken);
        foreach (var rule in rules)
            rule.CategoryName = targetName;

        var transactions = await _db.Transactions
            .Where(x => x.LedgerId == ledgerId && x.UserCategoryName != null && sourceNames.Contains(x.UserCategoryName))
            .ToListAsync(cancellationToken);
        foreach (var tx in transactions)
            tx.UserCategoryName = targetName;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task SetCategoryScopeAsync(string ledgerId, string categoryName, TransactionScope newScope, CancellationToken cancellationToken = default)
    {
        var rules = await _db.CategoryRules
            .Where(x => x.LedgerId == ledgerId && x.CategoryName == categoryName)
            .ToListAsync(cancellationToken);
        foreach (var rule in rules)
            rule.Scope = newScope;

        var transactions = await _db.Transactions
            .Where(x => x.LedgerId == ledgerId && x.UserCategoryName == categoryName)
            .ToListAsync(cancellationToken);
        foreach (var tx in transactions)
            tx.Scope = newScope;

        await _db.SaveChangesAsync(cancellationToken);
    }
}

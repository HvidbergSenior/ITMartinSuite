using ITMartinBudget.Application.Interfaces;
using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Services;

// One posting as the Kategorier page shows it. Pattern = the bank text the
// app groups by (several postings to the same person share one pattern).
public sealed record CategoryTransaction(
    int Id, DateTime Date, string Description, decimal Amount, string RawDetails, string Pattern,
    string? Category, TransactionScope Scope);

public sealed record CategoryLine(
    string Name, string Sub, int Count, decimal Sum, TransactionScope Scope,
    IReadOnlyDictionary<int, decimal> ByMonth);

// A group = the part before " · " in a category name ("Børn · Eigil" -> "Børn"),
// so own sub-categories fold under one heading.
public sealed record CategoryGroup(
    string Name, decimal Sum, int Count, IReadOnlyList<CategoryLine> Lines,
    IReadOnlyDictionary<int, decimal> ByMonth);

public sealed record CategoryOverview(
    IReadOnlyList<CategoryGroup> Spending, IReadOnlyList<CategoryGroup> Income,
    IReadOnlyList<int> Months, int Year);

public enum ScopeFilter { All, Private, Business }

// Use case behind /kategorier: categories folded in groups, each with its
// months, and the postings behind a category. "Spørg AI" is ONE call per
// press (never looped), and the answer is saved per pattern so it shows
// again without a new call.
public sealed class CategoryOverviewService
{
    public const string Separator = " · ";
    private readonly ICategoryRuleService _rules;
    // Made only when Spørg AI is pressed - the page still works with no AI key set.
    private readonly Func<IInvestigationService> _ai;

    public CategoryOverviewService(ICategoryRuleService rules, Func<IInvestigationService> ai)
    {
        _rules = rules;
        _ai = ai;
    }

    public async Task<CategoryOverview> GetOverviewAsync(string ledgerId, ScopeFilter filter, CancellationToken ct = default)
        => Build(await _rules.GetLedgerTransactionsAsync(ledgerId, ct), filter);

    public async Task<IReadOnlyList<CategoryTransaction>> GetTransactionsAsync(string ledgerId, string category, CancellationToken ct = default)
        => (await _rules.GetLedgerTransactionsAsync(ledgerId, ct))
            .Where(t => t.Category == category)
            .OrderByDescending(t => t.Date)
            .ToList();

    public async Task<IReadOnlyList<string>> GetCategoryNamesAsync(string ledgerId, CancellationToken ct = default)
        => (await _rules.GetLedgerTransactionsAsync(ledgerId, ct))
            .Where(t => t.Category is not null).Select(t => t.Category!).Distinct()
            .OrderBy(n => n, StringComparer.Create(new System.Globalization.CultureInfo("da-DK"), true)).ToList();

    // Move ONE posting, or every posting to the same payee (a rule, so new CSV files sort themselves too).
    public async Task MoveAsync(string ledgerId, CategoryTransaction tx, string category, bool wholePayee, CancellationToken ct = default)
    {
        category = Clean(category);
        if (category.Length == 0) throw new ArgumentException("Skriv en kategori.");
        var all = await _rules.GetLedgerTransactionsAsync(ledgerId, ct);
        var scope = ScopeFor(category, all);
        if (wholePayee) await _rules.AssignAsync(ledgerId, tx.Pattern, category, scope, ct);
        else await _rules.SetTransactionCategoryAsync(ledgerId, tx.Id, category, scope, ct);
    }

    // "  børn ·  eigil " -> "børn · eigil": one space around the dot, no double spaces.
    public static string Clean(string category) =>
        string.Join(Separator, (category ?? "").Split('·').Select(p => System.Text.RegularExpressions.Regex.Replace(p.Trim(), @"\s+", " ")).Where(p => p.Length > 0));

    // An existing category keeps its Privat/Forretning; a new one is Forretning only under the Forretning group.
    public static TransactionScope ScopeFor(string category, IEnumerable<CategoryTransaction> all)
    {
        var existing = all.FirstOrDefault(t => t.Category == category && t.Scope != TransactionScope.Unknown);
        if (existing is not null) return existing.Scope;
        return GroupOf(category) == "Forretning" ? TransactionScope.Business : TransactionScope.Private;
    }

    public Task<Dictionary<string, InvestigationResult>> GetSavedAnswersAsync(string ledgerId, CancellationToken ct = default)
        => _rules.GetInvestigationsAsync(ledgerId, ct);

    public async Task<InvestigationResult> AskAiAsync(string ledgerId, CategoryTransaction tx, CancellationToken ct = default)
    {
        var details = $"{tx.RawDetails} · {tx.Amount:0.00} kr · {tx.Date:dd.MM.yyyy} · kategori nu: {tx.Category}";
        var answer = await _ai().InvestigateAsync(tx.Description, details, 1, tx.Amount, ct);
        await _rules.SaveInvestigationAsync(ledgerId, tx.Pattern, answer, ct);
        return answer;
    }

    // Pure: grouping and month sums, tested without a database.
    public static CategoryOverview Build(IEnumerable<CategoryTransaction> all, ScopeFilter filter)
    {
        var rows = all.Where(t => t.Category is not null && !IsSavingsMove(t.Category!))
            .Where(t => filter == ScopeFilter.All
                || (filter == ScopeFilter.Private && t.Scope != TransactionScope.Business)
                || (filter == ScopeFilter.Business && t.Scope == TransactionScope.Business))
            .ToList();
        var year = rows.Count == 0 ? DateTime.Today.Year : rows.Max(t => t.Date).Year;
        rows = rows.Where(t => t.Date.Year == year).ToList();
        var months = rows.Select(t => t.Date.Month).Distinct().OrderBy(m => m).ToList();

        List<CategoryGroup> Groups(IEnumerable<CategoryTransaction> part, bool income) => part
            .GroupBy(t => GroupOf(t.Category!))
            .Select(g =>
            {
                var lines = g.GroupBy(t => t.Category!)
                    .Select(c => new CategoryLine(c.Key, SubOf(c.Key), c.Count(), c.Sum(t => t.Amount),
                        c.First().Scope, ByMonth(c)))
                    .OrderBy(l => income ? -l.Sum : l.Sum)
                    .ToList();
                return new CategoryGroup(g.Key, g.Sum(t => t.Amount), g.Count(), lines, ByMonth(g));
            })
            .OrderBy(g => income ? -g.Sum : g.Sum)
            .ToList();

        var incomeRows = rows.Where(t => IsIncome(t.Category!)).ToList();
        var spendRows = rows.Where(t => !IsIncome(t.Category!)).ToList();
        return new CategoryOverview(Groups(spendRows, false), Groups(incomeRows, true), months, year);
    }

    public static string GroupOf(string category)
    {
        var i = category.IndexOf(Separator, StringComparison.Ordinal);
        return i < 0 ? category : category[..i];
    }

    public static string SubOf(string category)
    {
        var i = category.IndexOf(Separator, StringComparison.Ordinal);
        return i < 0 ? category : category[(i + Separator.Length)..];
    }

    public static bool IsIncome(string category) => GroupOf(category) == "Indtægt";

    public static bool IsSavingsMove(string category) => category.StartsWith("Opsparing (flytning)", StringComparison.Ordinal);

    private static IReadOnlyDictionary<int, decimal> ByMonth(IEnumerable<CategoryTransaction> rows) =>
        rows.GroupBy(t => t.Date.Month).ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));
}

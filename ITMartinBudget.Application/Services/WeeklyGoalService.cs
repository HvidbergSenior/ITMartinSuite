using ITMartinBudget.Application.Interfaces;
using ITMartinBudget.Domain.Entities;

namespace ITMartinBudget.Application.Services;

// One goal as the page shows it: the goal, what this week has cost so far (Monday - today), last week, and a normal week.
public sealed record GoalStatus(string Category, decimal Goal, decimal ThisWeek, decimal LastWeek, decimal AverageWeek, DateTime WeekStart)
{
    public decimal Left => Goal - ThisWeek;
    public bool Over => ThisWeek > Goal;
    // 0-100 for the bar (a week over the goal stays at 100).
    public int Percent => Goal <= 0 ? 0 : (int)Math.Min(100, Math.Round(ThisWeek / Goal * 100));
}

// Use case behind the Ugemål card on /kategorier: set, change, remove a goal, and see each goal against this week.
public sealed class WeeklyGoalService
{
    private readonly IWeeklyGoalStore _store;
    private readonly ICategoryRuleService _rules;

    public WeeklyGoalService(IWeeklyGoalStore store, ICategoryRuleService rules)
    {
        _store = store;
        _rules = rules;
    }

    public async Task<IReadOnlyList<GoalStatus>> GetStatusAsync(string ledgerId, DateTime today, CancellationToken ct = default)
    {
        var goals = await _store.GetAsync(ledgerId, ct);
        if (goals.Count == 0) return [];
        return Status(goals, await _rules.GetLedgerTransactionsAsync(ledgerId, ct), today);
    }

    // Setting a goal for a category that already has one changes it.
    public async Task SetAsync(string ledgerId, string category, decimal krPerWeek, CancellationToken ct = default)
    {
        var goal = WeeklyGoal.Create(CategoryOverviewService.Clean(category), krPerWeek);
        var goals = (await _store.GetAsync(ledgerId, ct)).Where(g => !Same(g.Category, goal.Category)).Append(goal).ToList();
        await _store.SaveAsync(ledgerId, goals, ct);
    }

    public async Task RemoveAsync(string ledgerId, string category, CancellationToken ct = default)
    {
        var goals = (await _store.GetAsync(ledgerId, ct)).Where(g => !Same(g.Category, category)).ToList();
        await _store.SaveAsync(ledgerId, goals, ct);
    }

    // The year in two parts (user 2026-11-19: "focus on 2 parts, july to dd"): a normal week is measured only inside
    // the current half - 1 Jan-30 Jun or 1 Jul-today - so the summer's habits are not mixed with the spring's.
    public static DateTime HalfStart(DateTime today) => new(today.Year, today.Month <= 6 ? 1 : 7, 1);

    // Pure: weeks run Monday-Sunday. Refunds (plus amounts) count against the week they land in, so a friend paying
    // back his share of the beers lowers the week. Normal week = net spend in the full weeks of this half before this week.
    public static IReadOnlyList<GoalStatus> Status(IEnumerable<WeeklyGoal> goals, IEnumerable<CategoryTransaction> all, DateTime today)
    {
        var rows = all.Where(t => t.Category is not null).ToList();
        var weekStart = WeekStart(today);
        var lastWeekStart = weekStart.AddDays(-7);
        var half = HalfStart(today);
        var firstWeek = WeekStart(half) < half ? WeekStart(half).AddDays(7) : half;   // first full week inside the half
        var weeks = Math.Max(0, (int)((weekStart - firstWeek).TotalDays / 7));
        return goals.Select(g =>
        {
            var mine = rows.Where(t => Same(t.Category!, g.Category)).ToList();
            decimal Spent(DateTime from, DateTime to) => Math.Max(0, -mine.Where(t => t.Date.Date >= from && t.Date.Date < to).Sum(t => t.Amount));
            var normal = weeks == 0 ? 0 : Math.Round(Spent(firstWeek, weekStart) / weeks, 0);
            return new GoalStatus(g.Category, g.KrPerWeek, Spent(weekStart, today.Date.AddDays(1)), Spent(lastWeekStart, weekStart),
                normal, weekStart);
        }).OrderByDescending(s => s.Over).ThenBy(s => s.Category, StringComparer.Create(new System.Globalization.CultureInfo("da-DK"), true)).ToList();
    }

    private static DateTime WeekStart(DateTime d) => d.Date.AddDays(-(((int)d.DayOfWeek + 6) % 7));

    private static bool Same(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
}

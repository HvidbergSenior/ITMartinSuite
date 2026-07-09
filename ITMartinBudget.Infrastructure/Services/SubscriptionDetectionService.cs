using ITMartinBudget.Application.Interfaces;
using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ITMartinBudget.Infrastructure.Services;

public sealed class SubscriptionDetectionService : ISubscriptionDetectionService
{
    private readonly BudgetDbContext _db;

    public SubscriptionDetectionService(BudgetDbContext db)
    {
        _db = db;
    }

    public async Task<List<DetectedSubscription>> DetectAsync()
    {
        // Savings transfers happen at regular intervals too, but they're not
        // something to reconsider cancelling - same exclusion DashboardService
        // applies to its own aggregates.
        var expenses = await _db.Transactions
            .Where(x => x.Amount < 0 && x.BudgetGroup != BudgetGroup.OverførslerTilFraOpsparingsKonto)
            .OrderBy(x => x.Date)
            .ToListAsync();

        var results = new List<DetectedSubscription>();

        foreach (var group in expenses.GroupBy(x => Math.Round(x.Amount, 2)))
        {
            var ordered = group.OrderBy(x => x.Date).ToList();
            if (ordered.Count < 2) continue;

            var gaps = new List<int>();
            for (var i = 1; i < ordered.Count; i++)
                gaps.Add((ordered[i].Date.Date - ordered[i - 1].Date.Date).Days);

            var interval = ClassifyInterval(gaps);
            if (interval is null) continue;

            var last = ordered[^1];
            results.Add(new DetectedSubscription
            {
                Amount = Math.Abs(group.Key),
                IntervalLabel = interval,
                Occurrences = ordered.Count,
                LastChargedDate = last.Date,
                DaysSinceLastCharge = (DateTime.Now.Date - last.Date.Date).Days,
                SampleDescription = !string.IsNullOrWhiteSpace(last.Title) ? last.Title : last.Description,
                BudgetGroup = last.BudgetGroup
            });
        }

        return results.OrderByDescending(x => x.DaysSinceLastCharge).ToList();
    }

    // Requires most of the gaps between charges to cluster around a known
    // interval within a tolerance - one stray gap (a late or skipped charge)
    // doesn't disqualify it, but random one-off purchases that happen to
    // share an amount won't pass this.
    private static string? ClassifyInterval(List<int> gaps)
    {
        bool Within(int gap, int target, int tolerance) => Math.Abs(gap - target) <= tolerance;

        var threshold = Math.Max(1, (int)Math.Ceiling(gaps.Count * 0.6));

        if (gaps.Count(g => Within(g, 7, 2)) >= threshold) return "Ugentligt";
        if (gaps.Count(g => Within(g, 30, 4)) >= threshold) return "Månedligt";
        if (gaps.Count(g => Within(g, 365, 10)) >= threshold) return "Årligt";
        return null;
    }
}

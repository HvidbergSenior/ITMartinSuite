namespace ITMartinBudget.Domain.Entities;

// "Ugemål" (user 2026-11-19, for Bertil: "how much money for a week ... golf and padel, drinking beers, take away"):
// what one category may cost in a week, Monday-Sunday. One goal per category in a ledger.
public sealed record WeeklyGoal(string Category, decimal KrPerWeek)
{
    public static WeeklyGoal Create(string category, decimal krPerWeek)
    {
        if (string.IsNullOrWhiteSpace(category)) throw new ArgumentException("Vælg en kategori.");
        if (krPerWeek <= 0) throw new ArgumentException("Skriv et beløb over 0 kr.");
        return new WeeklyGoal(category.Trim(), Math.Round(krPerWeek));
    }
}

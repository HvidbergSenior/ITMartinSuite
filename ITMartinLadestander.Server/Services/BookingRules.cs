using ITMartinElPriser.Core;

namespace ITMartinLadestander.Server.Services;

public sealed record BookingSuggestion(int Outlet, DateTime StartDk, DateTime EndDk, double EstimatedKr);

// The rules for booking and the queue. Kept small on purpose: they are neighbour agreements shown in the app,
// the chargers themselves stay first come, first served.
public static class BookingRules
{
    public const int MaxHours = 12;
    public static readonly TimeSpan QueueExpiry = TimeSpan.FromHours(12);

    public static bool Overlaps(IEnumerable<Booking> bookings, int outlet, DateTime start, DateTime end, Guid? ignore = null) =>
        bookings.Any(b => b.Outlet == outlet && b.Id != ignore && b.StartDk < end && start < b.EndDk);

    public static TimeSpan Duration(double kwh, double kw) =>
        TimeSpan.FromMinutes(Math.Ceiling(Math.Max(0.25, kwh / Math.Max(1, kw)) * 4) * 15);

    // The cheapest start (15-minute grid) from now until `readyBy` on an outlet that is free for the whole run.
    // Priced as the household would pay: meter cost + tillæg incl. moms.
    public static BookingSuggestion? Cheapest(ForeningData f, List<PricePoint> raw, double kwh, DateTime readyBy, double carKw)
    {
        var now = DkTime.Now;
        var from = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute / 15 * 15, 0).AddMinutes(15);
        BookingSuggestion? best = null;
        foreach (var o in f.OutletSetup.Where(o => o.Source != "manual"))
        {
            var dur = Duration(kwh, Math.Min(o.MaxKw, carKw));
            for (var start = from; start + dur <= readyBy; start = start.AddMinutes(15))
            {
                if (Overlaps(f.Bookings, o.Number, start, start + dur)) continue;
                var (_, user) = SessionPricing.Price(raw, f, start, start + dur, kwh);
                if (user is null) continue;
                if (best is null || user < best.EstimatedKr - 0.005) best = new BookingSuggestion(o.Number, start, start + dur, user.Value);
            }
        }
        return best;
    }

    // Housekeeping every minute: drop finished bookings after a day, and queue entries that are 12 hours old or
    // whose household has started charging on any outlet.
    public static bool Tidy(ForeningData f, IReadOnlyList<OutletLive> live)
    {
        var now = DkTime.Now;
        var charging = live.Where(l => l.State is OutletState.Charging or OutletState.Connected && l.Token.Length > 0)
            .Select(l => f.HouseholdList.FirstOrDefault(h => h.Tokens.Contains(l.Token, StringComparer.OrdinalIgnoreCase))?.Id)
            .Where(id => id is not null).Select(id => id!.Value)
            .Concat(f.Sessions.Where(s => s.Active).Select(s => f.Cars.FirstOrDefault(c => c.Id == s.CarId)?.HouseholdId).Where(id => id is not null).Select(id => id!.Value))
            .ToHashSet();
        var removed = f.Bookings.RemoveAll(b => b.EndDk < now.AddDays(-1));
        removed += f.Queue.RemoveAll(q => now - q.JoinedDk > QueueExpiry || charging.Contains(q.HouseholdId));
        return removed > 0;
    }
}

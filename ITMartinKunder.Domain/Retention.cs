namespace ITMartinKunder.Domain;

// How long a customer is kept (user 2026-10-24, GDPR): while a yes stands - kept. Afmeldt = deleted after 30 days (the
// row is the proof they said stop until then). A purchase is kept 5 years after the last one (bogføringsloven), so a
// buyer stays that long even after afmeld. The same numbers are on itmartin.dk/vilkaar - change both together.
public static class Retention
{
    public const int UnsubscribedKeepDays = 30, PurchaseKeepYears = 5;

    public static bool ShouldDelete(Customer c, DateTime utcNow)
    {
        if (c.Consents.Any(x => x.WithdrawnAt is null) && c.Active) return false;   // a yes stands
        var lastBuy = c.Events.Where(e => e.Kind == EventKinds.Bought).Select(e => (DateTime?)e.At).Max();
        if (lastBuy is { } b && b > utcNow.AddYears(-PurchaseKeepYears)) return false;
        if (c.UnsubscribedAt is { } u) return u <= utcNow.AddDays(-UnsubscribedKeepDays);
        return lastBuy is not null;   // a buyer who never said yes: gone 5 years after the last purchase
    }
}

using ITMartinVarer.Contracts;

namespace ITMartinSalg.Domain;

/// <summary>One console lent for a weekend (2026-11-21). Ny = asked on bogshoppen.dk; Bekræftet = Martin said yes; Udleveret =
/// handed out; Afleveret = back; Aflyst = called off. The price is fixed at asking. No deposit (user 2026-11-21).</summary>
public sealed class ConsoleLoan
{
    public const string New = "Ny", Confirmed = "Bekræftet", Out = "Udleveret", Back = "Afleveret", Cancelled = "Aflyst";

    public int Id { get; set; }
    public string Customer { get; set; } = "";
    public string Console { get; set; } = "";
    public DateTime Friday { get; set; }
    public int PriceKr { get; set; }
    public bool WithTenDvds { get; set; }
    public string Status { get; set; } = New;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public static ConsoleLoan Ask(string? customer, string? console, DateTime friday, bool withTenDvds, DateTime now)
    {
        if (ConsoleLoanRules.For(console) is not { } c) throw new InvalidOperationException("Vælg PS3, PS4 eller PS5.");
        var day = friday.Date;
        if (day.DayOfWeek != DayOfWeek.Friday) throw new InvalidOperationException("Vælg en fredag – lånet er fra fredag til mandag.");
        if (day < now.Date) throw new InvalidOperationException("Den fredag er gået.");
        if (day > now.Date.AddDays(ConsoleLoanRules.MaxDaysAhead)) throw new InvalidOperationException($"Du kan bestille højst {ConsoleLoanRules.MaxDaysAhead} dage frem.");
        return new()
        {
            Customer = Kundenummer.Require(customer), Console = c.Console, Friday = day, WithTenDvds = withTenDvds && c.Console == ConsoleLoanRules.Ps3,
            PriceKr = ConsoleLoanRules.PriceFor(c.Console, withTenDvds)!.Value, CreatedAt = now, UpdatedAt = now,
        };
    }

    public void Confirm(DateTime now) => Move(New, Confirmed, now);
    public void HandOut(DateTime now) { if (Status == New) Status = Confirmed; Move(Confirmed, Out, now); }
    public void Return(DateTime now) => Move(Out, Back, now);

    public void Cancel(DateTime now)
    {
        if (Status is Out or Back or Cancelled) throw new InvalidOperationException("Lånet kan ikke aflyses nu.");
        (Status, UpdatedAt) = (Cancelled, now);
    }

    private void Move(string from, string to, DateTime now)
    {
        if (Status != from) throw new InvalidOperationException($"Lånet er {Status.ToLowerInvariant()}.");
        (Status, UpdatedAt) = (to, now);
    }
}

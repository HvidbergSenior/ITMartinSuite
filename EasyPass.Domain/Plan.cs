namespace EasyPass.Domain;

/// <summary>
/// The trip as a process, so the person never has to work it out alone:
/// 1 Før du tager af sted (questions to ask the place), 2 Tag med, 3 Ved indgangen (show Mit pas), 4 Bagefter (tell how it went).
/// </summary>
public sealed record TurPlan(IReadOnlyList<string> SpoergOm, IReadOnlyList<string> TagMed, string Raad);

public static class Planlaegger
{
    public static TurPlan Lav(Sted sted, IReadOnlyList<Behov> behov)
    {
        var match = Matcher.Vurder(sted, behov);
        var spoerg = new List<string>();
        var set = new HashSet<string>();
        foreach (var b in behov)
        {
            if (b.Kraever.Count == 0) { spoerg.Add($"{b.Tekst}. Hvordan kan I hjælpe?"); continue; }
            foreach (var k in b.Kraever)
                if (sted.Har(k) == Svar.Ukendt && set.Add(k) && Katalog.FindFacilitet(k) is { } f) spoerg.Add(f.Spoergsmaal);
        }

        var tagMed = behov.Select(b => b.TagMed).Where(t => t.Length > 0).Distinct().ToList();

        var raad = match.Pasning switch
        {
            Pasning.PasserIkke => $"Stedet mangler: {string.Join(", ", match.Mangler).ToLowerInvariant()}. Find et andet sted, eller ring og spørg, om de kan hjælpe på en anden måde.",
            Pasning.TjekSelv when spoerg.Count > 0 => "Ring eller skriv til stedet før du tager af sted, og stil spørgsmålene herunder. Så ved du, hvad du kommer ud til.",
            Pasning.TjekSelv => "Sæt kryds i Mit pas, så kan vi sige, om stedet passer til dig.",
            _ => "Stedet har det, du har brug for. God tur!",
        };
        return new TurPlan(spoerg, tagMed, raad);
    }
}

/// <summary>One step of the trip as a task in a shared room: what, a short note, which day (yyyy-MM-dd), clock time ("" = none).</summary>
public sealed record TurTrin(string Titel, string Note, string Dag, string Hvornaar = "");

public static class TurTrinRegler
{
    /// <summary>
    /// The plan as steps on days, for the person and their helper (pædagog, ledsager): call the place two days before,
    /// pack the day before, go on the day, and say how it went. A step that would land before today lands today.
    /// </summary>
    public static IReadOnlyList<TurTrin> Lav(Sted sted, IReadOnlyList<Behov> behov, DateOnly dato, DateOnly idag)
    {
        var plan = Planlaegger.Lav(sted, behov);
        string Dag(int foer) { var d = dato.AddDays(-foer); return (d < idag ? idag : d).ToString("yyyy-MM-dd"); }
        var trin = new List<TurTrin>();
        if (plan.SpoergOm.Count > 0)
            trin.Add(new($"Ring til {sted.Navn} og spørg", string.Join(" ", plan.SpoergOm) + (sted.Telefon.Length > 0 ? $" Telefon: {sted.Telefon}" : ""), Dag(2)));
        trin.Add(new("Pak til turen", string.Join(", ", new[] { "Telefonen med dit pas" }.Concat(plan.TagMed)), Dag(1)));
        var hvor = string.Join(", ", new[] { sted.Adresse, $"{sted.Postnr} {sted.By}".Trim() }.Where(x => x.Length > 0));
        trin.Add(new($"Tag af sted til {sted.Navn}", (hvor.Length > 0 ? hvor + ". " : "") + "Vis dit pas ved indgangen.", Dag(0)));
        trin.Add(new("Hvordan gik det?", "Skriv kort, hvordan det gik. Tryk også Det passede eller Noget passede ikke på stedets side i Easy-Pass.", Dag(0)));
        return trin;
    }
}

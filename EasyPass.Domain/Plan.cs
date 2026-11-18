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

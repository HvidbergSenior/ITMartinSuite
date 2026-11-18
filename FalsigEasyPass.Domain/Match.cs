namespace FalsigEasyPass.Domain;

/// <summary>Passer = everything you need is there. TjekSelv = nothing is missing, but some is not checked. PasserIkke = something you need is missing.</summary>
public enum Pasning { Passer, TjekSelv, PasserIkke }

/// <summary>The answer for one place and one "Mit pas": the verdict plus the reasons in plain Danish.</summary>
public sealed record MatchResultat(Pasning Pasning, IReadOnlyList<string> Godt, IReadOnlyList<string> Mangler, IReadOnlyList<string> Ukendt)
{
    public string Ord => Pasning switch
    {
        Pasning.Passer => "Passer til dig",
        Pasning.TjekSelv => "Tjek selv",
        _ => "Passer ikke",
    };
}

public static class Matcher
{
    /// <summary>
    /// For each ticked need, each facility it asks for: Ja = good, Nej = missing, Ukendt = check yourself.
    /// A need that asks for nothing (fx arm/hånd) is always "check yourself" - we never promise what nobody checked.
    /// No needs ticked = TjekSelv with no reasons (the page then asks you to fill in Mit pas).
    /// </summary>
    public static MatchResultat Vurder(Sted sted, IReadOnlyList<Behov> behov)
    {
        var godt = new List<string>();
        var mangler = new List<string>();
        var ukendt = new List<string>();
        var set = new HashSet<string>();

        foreach (var b in behov)
        {
            if (b.Kraever.Count == 0) { ukendt.Add($"{b.Tekst}: spørg stedet"); continue; }
            foreach (var key in b.Kraever)
            {
                if (!set.Add(key)) continue;
                var navn = Katalog.FindFacilitet(key)?.Navn ?? key;
                switch (sted.Har(key))
                {
                    case Svar.Ja: godt.Add(navn); break;
                    case Svar.Nej: mangler.Add(navn); break;
                    default: ukendt.Add(navn); break;
                }
            }
        }

        var pasning = mangler.Count > 0 ? Pasning.PasserIkke
            : ukendt.Count > 0 || behov.Count == 0 ? Pasning.TjekSelv
            : Pasning.Passer;
        return new MatchResultat(pasning, godt, mangler, ukendt);
    }
}

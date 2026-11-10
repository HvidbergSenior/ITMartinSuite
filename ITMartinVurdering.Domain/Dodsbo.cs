namespace ITMartinVurdering.Domain;

// Dødsbo og loft (user 2026-11-10: "something where I can value things from dødsboer or attics"): one Sag per place or person
// (fx "Mariannes ting"), a photo per thing, and per thing what it is, a price range and what to do with it. The AI reads several
// photos in ONE call under a daily cap (CLAUDE.md cost rules); a person can always write their own price and note.

/// <summary>What to do with a thing - the words the person sees.</summary>
public enum Verdict { Saelg, SaelgSamlet, GivVaek, SmidUd, SpoergEkspert }

public static class DodsboRules
{
    /// <summary>Photos read in one AI call.</summary>
    public const int PerCall = 8;
    public const int MaxText = 300, MaxName = 80, MaxStory = 1200;

    public static string Text(Verdict v) => v switch
    {
        Verdict.Saelg => "Sælg den",
        Verdict.SaelgSamlet => "Sælg sammen med andre",
        Verdict.GivVaek => "Giv den væk",
        Verdict.SmidUd => "Smid ud / genbrug",
        _ => "Spørg en ekspert",
    };

    /// <summary>Where it sells best - the AI picks one of these.</summary>
    public static readonly string[] Places = ["DBA", "Lauritz.com", "Bruun Rasmussen", "Loppemarked", "Bogshoppen", "Genbrugsbutik"];

    public static Verdict? ParseVerdict(string? s) => (s ?? "").Trim().ToLowerInvariant() switch
    {
        "sælg" or "saelg" => Verdict.Saelg,
        "sælg samlet" or "saelgsamlet" => Verdict.SaelgSamlet,
        "giv væk" or "givvaek" => Verdict.GivVaek,
        "smid ud" or "smidud" => Verdict.SmidUd,
        "spørg en ekspert" or "spoergekspert" => Verdict.SpoergEkspert,
        _ => null,
    };

    public static string Clean(string? s, int max) => (s ?? "").Trim() is var t && t.Length > max ? t[..max] : (s ?? "").Trim();

    /// <summary>"300–600 kr." / "ca. 400 kr." / "" - the range as the person reads it.</summary>
    public static string Range(int? low, int? high) => (low is > 0 ? low : null, high is > 0 ? high : null) switch
    {
        ({ } l, { } h) when l == h => $"ca. {l:N0} kr.",
        ({ } l, { } h) => $"{l:N0}–{h:N0} kr.",
        ({ } l, null) => $"fra {l:N0} kr.",
        (null, { } h) => $"op til {h:N0} kr.",
        _ => "",
    };
}

public sealed class Ting
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public DateTime AddedAt { get; set; }
    /// <summary>What the person wrote when the photo was taken (fx "står i kælderen", "mormors"), sent to the AI as a hint.</summary>
    public string Hint { get; set; } = "";
    public DateTime? ValuedAt { get; set; }
    public string What { get; set; } = "";
    public string Details { get; set; } = "";
    public int? LowKr { get; set; }
    public int? HighKr { get; set; }
    public Verdict? Verdict { get; set; }
    public string Where { get; set; } = "";
    public string Reason { get; set; } = "";
    /// <summary>What it is and the history behind it (user 2026-11-10: "description of the things found... the history behind") -
    /// maker, period, how it was made and used; comes in the same AI call, no extra cost.</summary>
    public string Story { get; set; } = "";
    /// <summary>Words to search for it on DBA / Lauritz (the buttons under the thing).</summary>
    public string SearchWords { get; set; } = "";
    /// <summary>The person's own price - wins over the AI's range.</summary>
    public int? OwnKr { get; set; }
    public string OwnNote { get; set; } = "";

    public bool Waiting => ValuedAt is null;

    public void Valued(string? what, string? details, int? low, int? high, Verdict verdict, string? where, string? reason, string? search, DateTime at,
        string? story = null)
    {
        Story = DodsboRules.Clean(story, DodsboRules.MaxStory);
        What = DodsboRules.Clean(what, DodsboRules.MaxName);
        Details = DodsboRules.Clean(details, DodsboRules.MaxText);
        (LowKr, HighKr) = (low is > 0 ? low : null, high is > 0 ? high : null);
        if (LowKr > HighKr) (LowKr, HighKr) = (HighKr, LowKr);
        Verdict = verdict;
        Where = DodsboRules.Places.FirstOrDefault(p => string.Equals(p, where?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? "";
        Reason = DodsboRules.Clean(reason, DodsboRules.MaxText);
        SearchWords = DodsboRules.Clean(search, DodsboRules.MaxName);
        ValuedAt = at;
    }
}

public sealed class Sag
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public string Name { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public List<Ting> Ting { get; set; } = [];

    public static Sag New(string? name, DateTime at)
    {
        var n = DodsboRules.Clean(name, DodsboRules.MaxName);
        if (n.Length == 0) throw new ArgumentException("Giv sagen et navn – fx Mariannes ting eller Loftet i Risskov.");
        return new Sag { Name = n, CreatedAt = at };
    }

    /// <summary>What the things worth selling add up to (own price first, else the AI's range); the rest is not counted.</summary>
    public (int Low, int High) Total()
    {
        int low = 0, high = 0;
        foreach (var t in Ting.Where(t => t.OwnKr is > 0 || t.Verdict is Domain.Verdict.Saelg or Domain.Verdict.SaelgSamlet))
        {
            if (t.OwnKr is { } own and > 0) { low += own; high += own; continue; }
            low += t.LowKr ?? t.HighKr ?? 0;
            high += t.HighKr ?? t.LowKr ?? 0;
        }
        return (low, high);
    }
}

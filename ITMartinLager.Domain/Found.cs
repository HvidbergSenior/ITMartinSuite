namespace ITMartinLager.Domain;

// What was read off a cover or looked up by barcode - not saved yet.
public sealed record Found(string Kind, string Title, string Artist, string Series, string Number, int? Year,
    string Platform, string Condition, int Quantity, double Confidence, string Note, string Interest = "", string Barcode = "", decimal? PriceHint = null, string EditionHint = "");

public sealed record BookValue(int? Low, int? High, string Demand, string Verdict, string Reason, string Where = "", string[]? Basis = null)
{
    public const string Unsure = "Tjek selv";
    public static readonly string[] Verdicts = ["Sælg", "Kasse", "Genbrug", Unsure];
    public static readonly string[] Demands = ["Høj", "Middel", "Lav"];
    public static readonly string[] Places = ["DBA", "AbeBooks", "Amazon.de", "eBay", "Bogshoppen", "Nemos Bibliotek", "Faraos"];
    public static readonly string[] Sources = ["DBA", "AbeBooks", "Amazon.de", "Nemos Bibliotek", "Faraos"];

    // What the AI answered, tidied: unknown words become the safe choice.
    public static BookValue Create(int? rawLow, int? rawHigh, string? demand, string? verdictText, string? reason, string? where, string[]? basis)
    {
        var verdict = Verdicts.FirstOrDefault(v => string.Equals(v, verdictText?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? Unsure;
        // Not sure = no price at all (user 2026-10-07: "dont give an estimate if you are not sure at all").
        int? low = verdict == Unsure || rawLow is null ? null : Math.Max(0, rawLow.Value);
        int? high = low is null ? null : Math.Max(low.Value, rawHigh ?? low.Value);
        return new(low, high,
            Demands.FirstOrDefault(d => string.Equals(d, demand?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? "Lav",
            verdict, (reason ?? "").Trim(),
            Places.FirstOrDefault(p => string.Equals(p, where?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? "",
            (basis ?? []).Select(b => Sources.FirstOrDefault(s => string.Equals(s, b?.Trim(), StringComparison.OrdinalIgnoreCase)))
                .OfType<string>().Distinct().ToArray());
    }

    // No price without proof (user 2026-10-07: "lots of books get 20-40 kr as default value ... it needs proof"):
    // a price stands only when a source the AI names as its basis really has prices for the book. Otherwise it is
    // "Tjek selv" with no price, and the AI's opinion stays in the reason.
    public BookValue Proven(Func<string, bool> hasPrices)
    {
        if (Verdict == Unsure || (Basis ?? []).Any(hasPrices)) return this;
        return this with { Low = null, High = null, Verdict = Unsure, Reason = "Ingen markedspris at vise som bevis. " + Reason };
    }
}

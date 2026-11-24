using System.Globalization;
using System.Text.RegularExpressions;
using System.Text.Json.Serialization;

namespace ITMartinElPriser.Infrastructure;

// Every grid company's household tariff from Energinet's DatahubPricelist (CC BY 4.0 since 2024-04-23:
// commercial use allowed with the credit "Kilde: Energinet"). Replaced Strømligning (CC BY-NC) 2026-11-24.
//
// A household (C customer) pays the row named "Nettarif C" / "Nettarif C time" plus any "Rabat Nettarif C"
// rows - checked 2026-11-24 for 32 of 36 grid companies; Konstant's winter result matches the meter's
// eloverblik charges. Production, local-collective, co-located and effect tariffs are other customer types.
// Prices are kr/kWh ex VAT, Price1 = 00-01 local time; a row with only Price1 is the same all day.
internal sealed class DatahubTariffBook
{
    private static readonly Regex BaseNote = new(@"^nettarif c( time)?$", RegexOptions.IgnoreCase);
    private static readonly Regex RebateNote = new(@"^rabat nettarif c\b", RegexOptions.IgnoreCase);
    private static readonly Regex OtherKind = new(@"produktion|lokal|indf|kollektiv|samplac|effekt|regional", RegexOptions.IgnoreCase);
    private static readonly Regex OwnerArea = new(@"\s*-\s*(\d{3})\s*$");

    internal sealed record Row(string Owner, string Code, bool Rebate, DateTime From, DateTime? To, double?[] Prices)
    {
        public bool ValidAt(DateTime t) => From <= t && (To is null || t < To);
        public double At(int hour) => Prices[hour] ?? Prices[0] ?? 0;
    }

    private readonly Dictionary<string, List<Row>> _byOwner;

    public DatahubTariffBook(IEnumerable<Row> rows)
    {
        _byOwner = rows.GroupBy(r => r.Owner).ToDictionary(g => g.Key, g => g.ToList());
    }

    public IReadOnlyCollection<string> Owners => _byOwner.Keys;
    public bool Has(string owner) => _byOwner.ContainsKey(owner);

    /// <summary>The DataHub row as the API returns it (only the columns asked for).</summary>
    internal sealed class Record
    {
        [JsonPropertyName("ChargeOwner")] public string? ChargeOwner { get; set; }
        [JsonPropertyName("ChargeTypeCode")] public string? ChargeTypeCode { get; set; }
        [JsonPropertyName("Note")] public string? Note { get; set; }
        [JsonPropertyName("ValidFrom")] public DateTime? ValidFrom { get; set; }
        [JsonPropertyName("ValidTo")] public DateTime? ValidTo { get; set; }
        [JsonPropertyName("Price1")] public double? P1 { get; set; }
        [JsonPropertyName("Price2")] public double? P2 { get; set; }
        [JsonPropertyName("Price3")] public double? P3 { get; set; }
        [JsonPropertyName("Price4")] public double? P4 { get; set; }
        [JsonPropertyName("Price5")] public double? P5 { get; set; }
        [JsonPropertyName("Price6")] public double? P6 { get; set; }
        [JsonPropertyName("Price7")] public double? P7 { get; set; }
        [JsonPropertyName("Price8")] public double? P8 { get; set; }
        [JsonPropertyName("Price9")] public double? P9 { get; set; }
        [JsonPropertyName("Price10")] public double? P10 { get; set; }
        [JsonPropertyName("Price11")] public double? P11 { get; set; }
        [JsonPropertyName("Price12")] public double? P12 { get; set; }
        [JsonPropertyName("Price13")] public double? P13 { get; set; }
        [JsonPropertyName("Price14")] public double? P14 { get; set; }
        [JsonPropertyName("Price15")] public double? P15 { get; set; }
        [JsonPropertyName("Price16")] public double? P16 { get; set; }
        [JsonPropertyName("Price17")] public double? P17 { get; set; }
        [JsonPropertyName("Price18")] public double? P18 { get; set; }
        [JsonPropertyName("Price19")] public double? P19 { get; set; }
        [JsonPropertyName("Price20")] public double? P20 { get; set; }
        [JsonPropertyName("Price21")] public double? P21 { get; set; }
        [JsonPropertyName("Price22")] public double? P22 { get; set; }
        [JsonPropertyName("Price23")] public double? P23 { get; set; }
        [JsonPropertyName("Price24")] public double? P24 { get; set; }

        public double?[] Prices() => [P1, P2, P3, P4, P5, P6, P7, P8, P9, P10, P11, P12, P13, P14, P15, P16, P17, P18, P19, P20, P21, P22, P23, P24];
    }

    /// <summary>Keeps only the household rows: base "Nettarif C" and its rebates.</summary>
    public static IEnumerable<Row> Household(IEnumerable<Record> records)
    {
        foreach (var r in records)
        {
            var note = (r.Note ?? "").Trim();
            if (r.ChargeOwner is not { Length: > 0 } owner || r.ValidFrom is not { } from) continue;
            if (OtherKind.IsMatch(note)) continue;
            var rebate = RebateNote.IsMatch(note);
            if (!rebate && !BaseNote.IsMatch(note)) continue;
            yield return new Row(owner.Trim(), r.ChargeTypeCode ?? "", rebate, from, r.ValidTo, r.Prices());
        }
    }

    /// <summary>Tariff (kr/kWh ex VAT) at local time t: the base row valid then plus its rebates. Past the last
    /// published row the latest one is used - tariffs follow a fixed daily pattern within a season.</summary>
    public double? At(string owner, DateTime t)
    {
        if (!_byOwner.TryGetValue(owner, out var rows)) return null;
        var bases = rows.Where(r => !r.Rebate).ToList();
        if (bases.Count == 0) return null;
        var b = bases.Where(r => r.ValidAt(t)).OrderBy(r => r.Code, StringComparer.Ordinal).FirstOrDefault()
                ?? bases.OrderByDescending(r => r.From).ThenBy(r => r.Code, StringComparer.Ordinal).First();
        var sum = b.At(t.Hour);
        // Rebates valid at t; if the base came from the fallback, its own period's rebates.
        var when = b.ValidAt(t) ? t : b.From;
        foreach (var codeRows in rows.Where(r => r.Rebate && r.ValidAt(when)).GroupBy(r => r.Code))
            sum += codeRows.OrderByDescending(r => r.From).First().At(t.Hour);
        return Math.Round(sum, 6);
    }

    /// <summary>The DataHub owner for a grid company found by Green Power Denmark: by area number where one company
    /// has several ("Konstant Net A/S - 151"), else by name ("Cerius A/S").</summary>
    public string? OwnerFor(string def, string company)
    {
        var byArea = Owners.FirstOrDefault(o => OwnerArea.Match(o) is { Success: true } m && m.Groups[1].Value == def);
        if (byArea is not null) return byArea;
        var key = Key(company);
        return Owners.FirstOrDefault(o => Key(OwnerArea.Replace(o, "")) == key);
    }

    /// <summary>Old Strømligning-era ids ("konstant_c") -> an owner, from the short name the household saved.</summary>
    public string? OwnerForShortName(string shortName)
    {
        var key = Key(shortName);
        if (key.Length < 2) return null;
        var hits = Owners.Where(o => Key(OwnerArea.Replace(o, "")).StartsWith(key, StringComparison.Ordinal)).ToList();
        return hits.Count == 0 ? null : hits.OrderBy(o => o.Length).First();
    }

    internal static string Key(string name) =>
        new string(name.ToLower(CultureInfo.GetCultureInfo("da-DK")).Where(char.IsLetterOrDigit).ToArray());
}

using System.Text.Json;
using System.Text.RegularExpressions;

namespace ITMartinTjek.Domain;

/// <summary>One line in a check: status is "ok", "warn", "bad" or "info".</summary>
public sealed record Check(string Id, string Name, string Status, string Value, string Hint);

/// <summary>"Hvad er galt?" - the advice for one run.</summary>
public sealed record Advice(string Diagnose, string[] Fint, string[] Problemer, string[] GoerNu, string[] KanFjernes);

// One saved check run. Browser and deep-script results are stored as the JSON the
// client produced, so the page can re-render an old run exactly as it looked.
public sealed class Snapshot
{
    public int Id { get; set; }
    public string Device { get; set; } = "";
    // The browser's secret code (2026-10-08): runs are only ever shown to, and deleted by, the browser that has it.
    // Old runs from before have "" and are shown to nobody.
    public string OwnerKey { get; set; } = "";
    public DateTime At { get; set; } = DateTime.UtcNow;
    // "browser" or "deep"
    public string Kind { get; set; } = SnapshotKind.Browser;
    public string Json { get; set; } = "[]";
    public int Ok { get; set; }
    public int Warn { get; set; }
    public int Bad { get; set; }
    public string Summary { get; set; } = "";
}

public static class SnapshotKind
{
    public const string Browser = "browser";
    public const string Deep = "deep";
}

public static class CheckRules
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public static int Rank(Check c) => c.Status switch { "bad" => 0, "warn" => 1, "ok" => 2, _ => 3 };

    public static (int Ok, int Warn, int Bad) Tally(IEnumerable<string?> statuses)
    {
        int ok = 0, warn = 0, bad = 0;
        foreach (var s in statuses)
            if (s == "ok") ok++; else if (s == "warn") warn++; else if (s == "bad") bad++;
        return (ok, warn, bad);
    }

    public static string Summary(int ok, int warn, int bad) => $"{ok} ok, {warn} gule, {bad} røde";

    /// <summary>The browser's code from tjek.js: 8-40 letters/digits. Without it nothing is saved or shown.</summary>
    public static string? OwnerKey(string? key)
    {
        var k = (key ?? "").Trim().ToUpperInvariant();
        return Regex.IsMatch(k, "^[A-Z0-9]{8,40}$") ? k : null;
    }

    /// <summary>A run made in this browser.</summary>
    public static Snapshot BrowserRun(string device, string ownerKey, IReadOnlyList<Check> checks)
    {
        var (ok, warn, bad) = Tally(checks.Select(c => c.Status));
        return new Snapshot
        {
            Device = device.Trim(), OwnerKey = ownerKey, Kind = SnapshotKind.Browser, Json = JsonSerializer.Serialize(checks),
            Ok = ok, Warn = warn, Bad = bad, Summary = Summary(ok, warn, bad),
        };
    }

    /// <summary>The checks in a saved run: browser runs are a JSON array, deep runs an object with "checks".</summary>
    public static List<Check> Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var el = doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement
                : doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("checks", out var c) ? c : default;
            return el.ValueKind == JsonValueKind.Array ? JsonSerializer.Deserialize<List<Check>>(el.GetRawText(), JsonOpts) ?? [] : [];
        }
        catch (JsonException) { return []; }
    }
}

// The device tests in the browser come back as raw numbers; these turn them into a check line with plain Danish.
public static class DeviceTests
{
    public static Check Mic(bool ok, string? error, string? label, int peak, int sampleRate) =>
        !ok ? new Check("mic", "Mikrofon", "bad", error ?? "Fejl", "Tjek at browseren har lov til mikrofonen, og at den rigtige mikrofon er valgt.")
        : peak < 3 ? new Check("mic", "Mikrofon", "bad", $"{label}: ingen lyd opfanget", "Mikrofonen er stum - forkert enhed valgt, slået fra (mute-knap), eller lydstyrken står på 0.")
        : peak < 15 ? new Check("mic", "Mikrofon", "warn", $"{label}: svag (top {peak} %)", "Meget lavt niveau - tal tættere på, eller skru op for mikrofonens indgangsniveau.")
        : new Check("mic", "Mikrofon", "ok", $"{label}: top {peak} %, {sampleRate} Hz", "");

    public static Check Tone(bool ok, string? error, bool sinkSupported) =>
        ok ? new Check("tone", "Testtone", "info", "Afspillet 1 sekund" + (sinkSupported ? "" : " (browseren kan ikke vælge udgang - brugte standard)"),
                "Hørte du intet? Tjek lydstyrke, mute og at den rigtige udgang er valgt i styresystemet.")
           : new Check("tone", "Testtone", "bad", error ?? "Fejl", "");

    public static Check Camera(bool ok, string? error, string? label, int? width, int? height, double? fps) =>
        ok ? new Check("cam", "Kamera", "ok", $"{label}: {width}×{height}" + (fps is { } f ? $" @ {f:0} fps" : ""), "")
           : new Check("cam", "Kamera", "bad", error ?? "Fejl", "Er kameraet i brug af et andet program, eller afvist i browseren?");
}

namespace ITMartinVarer.Contracts;

// Capture (2026-10-20, user: "One app for just taking pictures and storing ... select the relevant box number ... 100
// people on 100 boxes with own phone each"): Vareinfo only takes and keeps the photos and scans, box by box. Lager pulls
// what is new (GET /api/lager/nye), says what it has (POST /api/lager/modtaget), and fetches a photo when it reads it.
// Vareinfo never calls Lager.

public static class CaptureHeaders
{
    /// <summary>Lager's key on every call to Vareinfo's /api/lager/* (Lager__InfoKey there = Vareinfo__LagerKey here).</summary>
    public const string Key = "X-Info-Key";
}

/// <summary>A photo of things from one box. Tiles = close-ups next to it (GET /api/lager/foto/{Id}?del=0..Tiles-1).</summary>
public sealed record CapturedPhoto(int Id, string BoxCode, string Helper, int Tiles, DateTime TakenAt);

/// <summary>A barcode scanned into a box; Quantity = scanned that many times.</summary>
public sealed record CapturedScan(int Id, string BoxCode, string Helper, string Code, int Quantity, DateTime TakenAt);

/// <summary>The name Martin gave a box in a Vareinfo session ("Anders And 1970'erne") - Lager shows it on Kasser.</summary>
// Fund (2026-11-03): a session of things that are NOT ours yet - photographed where they are for sale (genbrugsplads),
// priced, and only the ones Martin takes home become stock. Lager reads its photos at once and keeps the things apart.
// Kind (2026-11-10, user: "These sessions could be all things magazines, strikke, ting, etc"): the button the person pressed
// ("Blade og album") - Lager tells the reader what the pile mostly is. ReadNow: a day session's photos are read at once (user
// 2026-11-10: "take pictures of my products -> and on Bogshoppen"), not first when someone presses Læs.
public sealed record CapturedBox(string BoxCode, string Name, bool Fund = false, string Kind = "", bool ReadNow = false);

/// <summary>One button on Vareinfo's start screen: what the pictures are of. Hint = what the reader is told the pile mostly is
/// ("" = it decides per thing). Strik and Ting are not here: they are put in one at a time in Lager (/strik, /ting).</summary>
public sealed record CaptureKind(string Name, string Hint);

public static class CaptureKinds
{
    public const string Mixed = "Blandet";

    public static readonly CaptureKind[] All =
    [
        new("Bøger", "bøger"),
        new("Blade og album", "tegneserier – album, serieblade, jumbobøger og superhelte"),
        new("Film", "film på DVD, Blu-ray og VHS"),
        new("Musik", "musik på CD, vinyl og kassettebånd"),
        new("Spil", "computerspil, brætspil og kortspil"),
        new("Rollespil", "rollespil som Dungeons & Dragons – bøger, bokse og terninger"),
        new("Magic-kort", "Magic: The Gathering-samlerkort"),
        new(Mixed, ""),
    ];

    public static bool IsKind(string? name) => All.Any(k => k.Name == name);

    /// <summary>What the reader is told for this button; "" for Blandet, an old session or an unknown name.</summary>
    public static string HintFor(string? name) => All.FirstOrDefault(k => k.Name == name)?.Hint ?? "";
}

public sealed record CaptureBatch(List<CapturedPhoto> Photos, List<CapturedScan> Scans, List<CapturedBox>? Boxes = null);

public sealed record CaptureReceived(List<int> PhotoIds, List<int> ScanIds);

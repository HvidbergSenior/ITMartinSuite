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

public sealed record CaptureBatch(List<CapturedPhoto> Photos, List<CapturedScan> Scans);

public sealed record CaptureReceived(List<int> PhotoIds, List<int> ScanIds);

namespace ITMartinVareinfo.Domain;

// Bogshoppen Vareinfo = the capture app (2026-10-20, user: "One app for just taking pictures and storing ... 100 people
// with own phone each"). Since 2026-10-29 everything is per session ("Session 3 – Boxit, reol 2"), no boxes: a helper
// taps a session and takes photos. Nothing is read here - Lager fetches the photos and reads them when Martin says so.
// The shared words (BoxCodes, Barcodes, CapturedPhoto ...) are in ITMartinVarer.Contracts.

/// <summary>Who took it: a helper, or Martin (Id 0, the main PIN).</summary>
public sealed record Taker(int Id, string Name)
{
    public bool IsAdmin => Id == 0;
}

/// <summary>A person who takes photos, with their own code (made on the Hjælpere page, one per person).</summary>
public sealed class Helper
{
    public const int CodeLength = 6;

    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public bool Active { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Taker AsTaker => new(Id, Name);

    /// <summary>Six digits - easy to type on a phone, and one in a million to guess.</summary>
    public static string NewCode(Random random) => random.Next(0, 1_000_000).ToString("D6");
}

/// <summary>A photo taken in a session. Uploaded as it is (fast, so 100 phones are never kept waiting) and made
/// ready in the background: a shrunk copy for reading plus close-ups (Path with -t0..-t3). The original is not kept.</summary>
public sealed class Photo
{
    public const int MaxAttempts = 3;

    public int Id { get; set; }
    public int SessionId { get; set; }
    public string BoxCode { get; set; } = "";    // the session's code for Lager (WorkSession.BoxCode)
    public int HelperId { get; set; }
    public string Helper { get; set; } = "";
    public string RawPath { get; set; } = "";    // the upload, until it is made ready (relative to the data dir)
    public string Path { get; set; } = "";       // the reading copy; "" = could not be opened
    public int Tiles { get; set; }
    public bool Ready { get; set; }
    public int Attempts { get; set; }
    public DateTime TakenAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }        // Lager has it

    public static string TilePath(string path, int n) => System.IO.Path.ChangeExtension(path, null) + $"-t{n}.jpg";

    public void MadeReady(string path, int tiles)
    {
        Path = path;
        Tiles = tiles;
        Ready = true;
        RawPath = "";
    }

    /// <summary>Could not be made ready. After MaxAttempts it is given up (Lager sees a photo that is gone).</summary>
    public void Failed()
    {
        Attempts++;
        if (Attempts >= MaxAttempts) { Ready = true; Path = ""; Tiles = 0; }
    }

    public CapturedPhoto ToCaptured() => new(Id, BoxCode, Helper, Tiles, TakenAt);
}

/// <summary>A barcode scanned in a session ("beep beep beep"). The same code again in the same session, before Lager
/// has it, = one more copy.</summary>
public sealed class Scan
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public int SessionId { get; set; }
    public string BoxCode { get; set; } = "";    // the session's code for Lager
    public int HelperId { get; set; }
    public string Helper { get; set; } = "";
    public int Quantity { get; set; } = 1;
    public DateTime TakenAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }

    public CapturedScan ToCaptured() => new(Id, BoxCode, Helper, Code, Quantity, TakenAt);
}

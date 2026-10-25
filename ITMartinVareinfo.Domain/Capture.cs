namespace ITMartinVareinfo.Domain;

// Bogshoppen Vareinfo = the capture app (2026-10-20, user: "One app for just taking pictures and storing ... The users
// of that app should easily take pictures of all and select the relevant box number ... 100 people on 100 boxes with
// own phone each"). A helper types the number written on the box, takes photos of everything in it and scans barcodes,
// and presses "Kassen er færdig". Nothing is read here - Lager fetches the photos and reads them when Martin says so.
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

/// <summary>Who is filling a box right now. One helper per box: a second one gets "Kasse 7 bruges af Rico". A box left
/// without a photo for Idle is free again (a helper who went home without pressing "Kassen er færdig").</summary>
public sealed class BoxClaim
{
    public static readonly TimeSpan Idle = TimeSpan.FromHours(2);
    public const int MaxNumber = 9999;
    /// <summary>The highest number a helper can type - above it are the personal test boxes.</summary>
    public const int MaxNormal = BoxCodes.FirstTest - 1;

    public string BoxCode { get; set; } = "";    // K0007 = NR 7 written on the box
    public int HelperId { get; set; }
    public string HelperName { get; set; } = "";
    public DateTime ClaimedAt { get; set; }
    public DateTime LastAt { get; set; }         // the latest photo or scan
    public DateTime? DoneAt { get; set; }        // "Kassen er færdig"

    public int Number => BoxCodes.Number(BoxCode);
    public bool IsTest => BoxCodes.IsTest(BoxCode);

    /// <summary>Someone else is filling it right now.</summary>
    public bool BusyFor(int helperId, DateTime now) => DoneAt is null && HelperId != helperId && now - LastAt < Idle;

    public bool OpenFor(int helperId, DateTime now) => DoneAt is null && HelperId == helperId && now - LastAt < Idle;

    public void Take(Taker who, DateTime now)
    {
        HelperId = who.Id;
        HelperName = who.Name;
        ClaimedAt = LastAt = now;
        DoneAt = null;
    }

    public void Finish(DateTime now) => DoneAt = now;
}

/// <summary>A photo of things from one box. Uploaded as it is (fast, so 100 phones are never kept waiting) and made
/// ready in the background: a shrunk copy for reading plus close-ups (Path with -t0..-t3). The original is not kept.</summary>
public sealed class Photo
{
    public const int MaxAttempts = 3;

    public int Id { get; set; }
    public string BoxCode { get; set; } = "";
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

/// <summary>A barcode scanned into a box ("beep beep beep. next box"). The same code again in the same box, before
/// Lager has it, = one more copy.</summary>
public sealed class Scan
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string BoxCode { get; set; } = "";
    public int HelperId { get; set; }
    public string Helper { get; set; } = "";
    public int Quantity { get; set; } = 1;
    public DateTime TakenAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }

    public CapturedScan ToCaptured() => new(Id, BoxCode, Helper, Code, Quantity, TakenAt);
}

/// <summary>One box on the overview: who has it, how far, done or not.</summary>
public sealed record BoxStatus(string BoxCode, string Helper, int Photos, int Scans, DateTime? LastAt, bool Done, bool Busy)
{
    public int Number => BoxCodes.Number(BoxCode);
}

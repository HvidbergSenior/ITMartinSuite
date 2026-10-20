namespace ITMartinVareinfo.Domain;

// The shared words (Kinds, Conditions, EditionHints, BoxCodes, Barcodes, Found, KolofonReading) are in
// ITMartinVarer.Contracts - Vareinfo and Lager meet only as JSON in those shapes.

// One photo of a pile (kept, so a wrong reading can be checked against the picture). Lager's items point to it by Id.
// Box photos (user 2026-10-15: "Take picture -> storage -> when time comes"): taken box by box and only STORED - Waiting
// until the person presses Læs, then the reader job reads it and puts what it found in that box on Lager.
public sealed class Photo
{
    public int Id { get; set; }
    public string Path { get; set; } = "";       // relative to the data dir
    public int? BoxId { get; set; }              // Lager's box id at the time
    public string BoxCode { get; set; } = "";    // Lager's box code (K0007 = "NR 7" on the box) - box photos only
    public int Tiles { get; set; }               // close-ups kept next to it (Path with -t0..-t3), cut from the full-size photo
    public bool Waiting { get; set; }            // stored, not read yet
    public bool ReadWanted { get; set; }         // the person pressed Læs - the job takes it
    public int Found { get; set; }
    public DateTime TakenAt { get; set; } = DateTime.UtcNow;

    public static string TilePath(string path, int n) => System.IO.Path.ChangeExtension(path, null) + $"-t{n}.jpg";

    public void Read(int found)
    {
        Found = found;
        Waiting = ReadWanted = false;
    }
}

/// <summary>Box photos per box: how many wait, how many the job will read, how many are read and what they held.
/// Scans = barcodes scanned into the box and not looked up yet (ScansWanted of them pressed Læs).</summary>
public sealed record BoxPhotos(string BoxCode, int Waiting, int Wanted, int Read, int Found, int Scans = 0, int ScansWanted = 0)
{
    public int Number => BoxCodes.Number(BoxCode);
    public bool HasUnread => Waiting > Wanted || Scans > ScansWanted;
}

// A barcode scanned into a box (user 2026-10-15: "scanning should just beep beep beep. next box"): only stored - the
// lookup (free sites, no AI) happens when the person presses Læs, together with the box's photos. The same code
// scanned again in the same box = one more copy.
public sealed class Scan
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public int BoxId { get; set; }
    public string BoxCode { get; set; } = "";
    public int Quantity { get; set; } = 1;
    public bool Waiting { get; set; } = true;
    public bool ReadWanted { get; set; }
    public bool Known { get; set; }              // the lookup found what it is
    public DateTime TakenAt { get; set; } = DateTime.UtcNow;

    /// <summary>Not found anywhere: still saved, so it is in the box - a person fills in the title in Lager.</summary>
    public static Found Unknown(string code) => new(Barcodes.IsIsbn(code) ? Kinds.Bog : Kinds.Andet, $"Ukendt – stregkode {code}",
        "", "", "", null, "", "God", 1, 0, "Stregkoden blev ikke fundet – skriv titlen", Barcode: code);
}

// A kolofon photo of one of Lager's books, waiting to be read or already read (up to 10 per AI call).
public sealed class KolofonShot
{
    public const int PerCall = 10;

    public int Id { get; set; }
    public int ItemId { get; set; }              // Lager's item id
    public string Title { get; set; } = "";
    public string Path { get; set; } = "";
    public DateTime TakenAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }        // null = waiting
    public string Result { get; set; } = "";     // what was read, as a line - shown on the kolofon page

    public void Read(KolofonReading? r, DateTime now)
    {
        ReadAt = now;
        Result = r is null ? "Kunne ikke læses"
            : string.Join(" · ", new[] { r.Edition, r.Publisher, r.Year?.ToString(), r.Isbn is { Length: > 0 } i ? "ISBN " + i : null, r.Note }
                .Where(x => !string.IsNullOrWhiteSpace(x)));
    }
}

// AI calls per day - the hard cap on spend lives in the database, so a restart does not reset it.
public sealed class AiDay
{
    public DateOnly Day { get; set; }
    public int Calls { get; set; }
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
}

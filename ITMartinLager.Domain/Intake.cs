namespace ITMartinLager.Domain;

// Intake (2026-10-20, user: "One app for just taking pictures and storing"): the helpers' photos and scans live in
// Vareinfo; Lager fetches what is new, box by box, and reads it only when the person presses Læs (one AI call per pile
// photo, under the daily cap). What was read goes into that box - Lager is where it is checked and priced.

/// <summary>A pile photo taken in Vareinfo. VareinfoId = its id there (the photo itself stays there).</summary>
/// <summary>When the AI could not really read a photo (user 2026-11-15: "if picture is too bad and nothing can be read... It
/// should not fill it out"). Measured on 437 photos the same day: blur and darkness do not predict it (the blurriest were still
/// read at 0.6-0.7), the AI's own confidence does. Below CannotRead nothing is made - the photo is listed as "kunne ikke læses".
/// 0.35-0.59 is made and goes to "Ret de svære".</summary>
public static class Readings
{
    public const double CannotRead = 0.35;
    public static bool Readable(Found f) => f.Confidence >= CannotRead;
    /// <summary>How long a photo that could not be read is listed on the front page.</summary>
    public static readonly TimeSpan ListedFor = TimeSpan.FromDays(7);
}

/// <summary>What the AI said about one pile photo. Retake = why it must be taken again (user 2026-11-15: "if the picture is
/// rotated and so on it should be taken again") - then nothing is made from it, whatever it thought it saw.</summary>
public sealed record PileReading(List<Found> Items, string Retake = "")
{
    public bool TakeAgain => Retake.Trim() != "";
    /// <summary>The things to make: none when the photo must be taken again, else only what could really be read.</summary>
    public List<Found> Usable => TakeAgain ? [] : Items.Where(Readings.Readable).ToList();
}

/// <summary>The Batch API (user 2026-11-15: "i would rather take a lot of pictures. And then api on as many as possible fewer
/// calls" -> "i want batch"): the day's photos are read together at half price. Sent at SendHour (Danish time) or as soon as
/// Max are waiting; the answers come back within the hour as a rule (at most 24 hours).</summary>
public static class AiBatching
{
    public const int SendHour = 21, Max = 100;

    public static bool Due(DateTime utcNow, DateTime? lastSentUtc, int waiting)
    {
        if (waiting == 0) return false;
        if (waiting >= Max) return true;
        var now = TimeZoneInfo.ConvertTimeFromUtc(utcNow, Copenhagen);
        if (now.Hour < SendHour) return false;
        if (lastSentUtc is not { } last) return true;
        var sent = TimeZoneInfo.ConvertTimeFromUtc(last, Copenhagen);
        return sent < now.Date.AddHours(SendHour);   // nothing sent since this evening's send time
    }

    private static readonly TimeZoneInfo Copenhagen = Find();
    private static TimeZoneInfo Find()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time"); }
    }
}

public sealed class BoxShot
{
    public int Id { get; set; }
    public int VareinfoId { get; set; }
    public string BoxCode { get; set; } = "";
    public string Helper { get; set; } = "";
    public int Tiles { get; set; }               // close-ups Vareinfo keeps next to it
    public DateTime TakenAt { get; set; }
    public bool Wanted { get; set; }             // the person pressed Læs - the job takes it
    public DateTime? ReadAt { get; set; }        // null = not read yet
    public int Found { get; set; }
    /// <summary>"drejet", "uskarpt" ... - the AI's reason to take the photo again; "" = it was fine (2026-11-15).</summary>
    public string Retake { get; set; } = "";
    /// <summary>The Batch API batch it is in ("" = not sent yet), and when it was sent (2026-11-15).</summary>
    public string BatchId { get; set; } = "";
    public DateTime? BatchedAt { get; set; }

    public void Read(int found, DateTime now, string retake = "")
    {
        Found = found;
        Retake = retake.Trim();
        ReadAt = now;
        Wanted = false;
    }
}

/// <summary>A barcode scanned into a box in Vareinfo; looked up (free sites, no AI) on Læs.</summary>
/// <summary>A kolofon page (or back cover) read with a box's photos that found no book yet (2026-11-03: the TestMartin test took the
/// kolofon of "Sallies historier" BEFORE its cover). It waits in its box and goes on the book when the cover has been read.</summary>
public sealed class KolofonPage
{
    public int Id { get; set; }
    public int BoxId { get; set; }
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public string Isbn { get; set; } = "";
    public string Edition { get; set; } = "";
    public int? Year { get; set; }
    public string Publisher { get; set; } = "";
    public string Note { get; set; } = "";
    public string PhotoPath { get; set; } = "";
    public DateTime ReadAt { get; set; }
}

public sealed class BoxScan
{
    public int Id { get; set; }
    public int VareinfoId { get; set; }
    public string BoxCode { get; set; } = "";
    public string Helper { get; set; } = "";
    public string Code { get; set; } = "";
    public int Quantity { get; set; } = 1;
    public DateTime TakenAt { get; set; }
    public bool Wanted { get; set; }
    public DateTime? ReadAt { get; set; }
    public bool Known { get; set; }              // the lookup found what it is

    public void Read(bool known, DateTime now)
    {
        Known = known;
        ReadAt = now;
        Wanted = false;
    }

}

/// <summary>Per box: photos and scans waiting (Wanted of them pressed Læs), read, and what the read photos held.</summary>
public sealed record BoxIntake(string BoxCode, int Waiting, int Wanted, int Read, int Found, int Scans, int ScansWanted, string Helpers, int Unknown = 0)
{
    public int Number => BoxCodes.Number(BoxCode);
    public bool HasUnread => Waiting > Wanted || Scans > ScansWanted;
}

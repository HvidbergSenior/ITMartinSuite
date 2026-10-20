namespace ITMartinLager.Domain;

// Intake (2026-10-20, user: "One app for just taking pictures and storing"): the helpers' photos and scans live in
// Vareinfo; Lager fetches what is new, box by box, and reads it only when the person presses Læs (one AI call per pile
// photo, under the daily cap). What was read goes into that box - Lager is where it is checked and priced.

/// <summary>A pile photo taken in Vareinfo. VareinfoId = its id there (the photo itself stays there).</summary>
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

    public void Read(int found, DateTime now)
    {
        Found = found;
        ReadAt = now;
        Wanted = false;
    }
}

/// <summary>A barcode scanned into a box in Vareinfo; looked up (free sites, no AI) on Læs.</summary>
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

    /// <summary>Not found anywhere: still saved, so it is in the box - a person fills in the title.</summary>
    public static Found Unknown(string code) => new(Barcodes.IsIsbn(code) ? Kinds.Bog : Kinds.Andet, $"Ukendt – stregkode {code}",
        "", "", "", null, "", "God", 1, 0, "Stregkoden blev ikke fundet – skriv titlen", Barcode: code);
}

/// <summary>Per box: photos and scans waiting (Wanted of them pressed Læs), read, and what the read photos held.</summary>
public sealed record BoxIntake(string BoxCode, int Waiting, int Wanted, int Read, int Found, int Scans, int ScansWanted, string Helpers)
{
    public int Number => BoxCodes.Number(BoxCode);
    public bool HasUnread => Waiting > Wanted || Scans > ScansWanted;
}

namespace FalsigForening.Domain;

// The association's own details (tjeklisten 2: "Foreningen"). Same fields as the forening table in Jacob's Foreningsmakker
// (supabase/migrations/0001_datamodel.sql + moede_varsel_dage), so a later import is one column to one property.
// One per group: the group (Club's team) IS the association; this is what makes it a forening.
public sealed class ForeningProfil : ITeamOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }

    public string Cvr { get; set; } = "";
    public string Kommune { get; set; } = "";
    public ForeningsType Type { get; set; } = ForeningsType.Anden;
    public string Formaal { get; set; } = "";
    public int? Medlemstal { get; set; }
    public int? MedlemmerUnder25 { get; set; }

    /// <summary>The month the accounting year starts (1-12).</summary>
    public int RegnskabsaarStartMd { get; set; } = 1;
    /// <summary>The month of the annual general meeting (1-12), if known.</summary>
    public int? GeneralforsamlingMd { get; set; }
    public string Tegningsregel { get; set; } = "";

    // What the association does - these decide which rules and grants apply to it later (tjeklisten 4).
    public bool Folkeoplysende { get; set; }
    public bool ModtagerKommunaltTilskud { get; set; }
    public bool HarBoernUnder15 { get; set; }
    public bool HarAnsatte { get; set; }
    public bool HarSalg { get; set; }
    public bool EjerBygning { get; set; }

    public string Hjemmeside { get; set; } = "";
    public string Email { get; set; } = "";
    public DateOnly? CvrRegistreret { get; set; }
    /// <summary>Days of notice before a board meeting.</summary>
    public int MoedeVarselDage { get; set; } = 7;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public static ForeningProfil NewFor(Guid groupId) => new() { GroupId = groupId };

    /// <summary>Takes the public CVR details. Only fills e-mail when none is typed - what the board typed wins.</summary>
    public void FillFromCvr(CvrDetails cvr)
    {
        Cvr = cvr.Cvr;
        CvrRegistreret = cvr.StartDate ?? CvrRegistreret;
        if (Email.Length == 0 && !string.IsNullOrWhiteSpace(cvr.Email)) Email = cvr.Email.Trim();
    }
}

/// <summary>Public company details from the CVR register. No contact person (that is personal data we do not need).</summary>
public sealed record CvrDetails(string Cvr, string Name, string Address, string PostalCode, string City, DateOnly? StartDate,
    string? CompanyForm, string? Email, bool Ceased);

public enum ForeningsType { Borgerforening, Forsamlingshus, Lokalhistorisk, Kultur, Hobby, Social, Idraet, Spejder, Patient, Aeldre, Anden }

public static class ForeningRules
{
    public static string Label(ForeningsType t) => t switch
    {
        ForeningsType.Borgerforening => "Borgerforening",
        ForeningsType.Forsamlingshus => "Forsamlingshus",
        ForeningsType.Lokalhistorisk => "Lokalhistorisk forening",
        ForeningsType.Kultur => "Kultur",
        ForeningsType.Hobby => "Hobby",
        ForeningsType.Social => "Social forening",
        ForeningsType.Idraet => "Idræt",
        ForeningsType.Spejder => "Spejder",
        ForeningsType.Patient => "Patientforening",
        ForeningsType.Aeldre => "Ældreforening",
        _ => "Anden",
    };

    public static readonly string[] Months =
        ["januar", "februar", "marts", "april", "maj", "juni", "juli", "august", "september", "oktober", "november", "december"];

    /// <summary>A CVR number is 8 digits; spaces are allowed while typing. "" = not a CVR number.</summary>
    public static string CleanCvr(string? text)
    {
        var digits = new string((text ?? "").Where(c => !char.IsWhiteSpace(c)).ToArray());
        return digits.Length == 8 && digits.All(char.IsAsciiDigit) ? digits : "";
    }

    /// <summary>"foreningen.dk" -> "https://foreningen.dk". Empty stays empty.</summary>
    public static string CleanWebsite(string? text)
    {
        var t = (text ?? "").Trim();
        if (t.Length == 0) return "";
        return t.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || t.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? t : "https://" + t;
    }

    /// <summary>Everything that is wrong, in Danish, so the board can fix it. Empty = it can be saved.</summary>
    public static List<string> Problems(string name, ForeningProfil p)
    {
        var problems = new List<string>();
        if (string.IsNullOrWhiteSpace(name)) problems.Add("Skriv foreningens navn.");
        if (p.Cvr.Length > 0 && CleanCvr(p.Cvr).Length == 0) problems.Add("CVR-nummeret skal være 8 cifre.");
        if (p.RegnskabsaarStartMd is < 1 or > 12) problems.Add("Vælg den måned, regnskabsåret starter.");
        if (p.GeneralforsamlingMd is < 1 or > 12) problems.Add("Vælg en måned for generalforsamlingen.");
        if (p.Medlemstal < 0 || p.MedlemmerUnder25 < 0) problems.Add("Antal medlemmer kan ikke være under 0.");
        if (p.Medlemstal is { } all && p.MedlemmerUnder25 is { } young && young > all)
            problems.Add("Der kan ikke være flere medlemmer under 25 end medlemmer i alt.");
        if (p.Email.Length > 0 && !p.Email.Contains('@')) problems.Add("Mailadressen ser forkert ud.");
        if (p.MoedeVarselDage is < 0 or > 90) problems.Add("Varsel til møder skal være mellem 0 og 90 dage.");
        return problems;
    }
}

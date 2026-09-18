namespace ITMartinForloebet.Server.Data.Entities;

public enum Udfald { Aabent = 0, Frafaldet = 1, Betalt = 2, Delvist = 3, Afvist = 4, Andet = 5 }

public enum Part { Borger = 0, Modpart = 1, Anden = 2 }

/// <summary>One person's forløb against one counterpart. Everything public on it
/// is AI-rewritten text the owner approved; the raw input is never stored.</summary>
public sealed class Forloeb
{
    public int Id { get; set; }
    public string Slug { get; set; } = "";       // public id in /sag/{slug}
    public string EditKey { get; set; } = "";    // secret; owner's link carries ?k=
    public string Titel { get; set; } = "";      // "Kontrolafgift – var ikke i bussen"
    public string Modpart { get; set; } = "";    // "Movia"
    public string Omraade { get; set; } = "";    // "Kontrolafgift"
    public DateTime StartedAt { get; set; }      // first event (user-given)
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public Udfald Udfald { get; set; }
    public string UdfaldTekst { get; set; } = "";  // "Sådan endte det"
    public string Laering { get; set; } = "";      // "Hvad lærte jeg"
    public bool Delt { get; set; }                 // "del anonymiseret" -> on the front page
    public string AnalyseJson { get; set; } = "";  // Analyse (six questions), AI
    public string Resume { get; set; } = "";       // 3-4 lines, AI, shown on the card
    public List<Trin> Trin { get; set; } = [];
    public List<Dokument> Dokumenter { get; set; } = [];

    public bool Lukket => ClosedAt is not null;
    public int Uger => (int)Math.Round(((ClosedAt ?? DateTime.UtcNow) - StartedAt).TotalDays / 7.0);
}

public sealed class Trin
{
    public int Id { get; set; }
    public int ForloebId { get; set; }
    public Forloeb Forloeb { get; set; } = null!;
    public DateTime At { get; set; }
    public Part Part { get; set; }
    public string Tekst { get; set; } = "";   // approved rewrite
    public string Citat { get; set; } = "";   // the decisive sentence, verbatim, if any
    public DateTime CreatedAt { get; set; }
}

/// <summary>Owner-only evidence (mail, brev, foto). Never served publicly.</summary>
public sealed class Dokument
{
    public int Id { get; set; }
    public int ForloebId { get; set; }
    public Forloeb Forloeb { get; set; } = null!;
    public int? TrinId { get; set; }
    public string FileName { get; set; } = "";
    public string StoredName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long Size { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Single row: the cached AI headline on the front page.</summary>
public sealed class SiteState
{
    public int Id { get; set; }
    public string Overskrift { get; set; } = "";
    public string OverskriftHash { get; set; } = "";
    public DateTime UpdatedAt { get; set; }
}

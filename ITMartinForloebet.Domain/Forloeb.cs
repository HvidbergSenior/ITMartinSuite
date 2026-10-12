using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ITMartinForloebet.Domain;

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

    // ---- the rules for changing a forløb: only with the owner's key, and the timeline only while it is open ----

    public bool HarNoegle(string? key) => !string.IsNullOrEmpty(key) && key == EditKey;

    /// <summary>A new forløb from the approved rewrite of the first message.</summary>
    public static Forloeb Opret(Omskrivning o, DateTime startedAt, bool delt, DateTime now)
    {
        var omraade = string.IsNullOrWhiteSpace(o.Omraade) ? "Andet" : o.Omraade.Trim();
        var modpart = string.IsNullOrWhiteSpace(o.Modpart) ? "Ukendt modpart" : o.Modpart.Trim();
        return new Forloeb
        {
            Slug = Slugs.ForForloeb(omraade, modpart),
            EditKey = Slugs.EditKey(),
            Titel = string.IsNullOrWhiteSpace(o.Titel) ? omraade : o.Titel.Trim(),
            Modpart = modpart,
            Omraade = omraade,
            StartedAt = startedAt,
            CreatedAt = now,
            UpdatedAt = now,
            Delt = delt,
            Trin = [new Trin { At = startedAt, Part = ForloebRules.ParsePart(o.Part), Tekst = o.Tekst.Trim(), Citat = o.Citat.Trim(), CreatedAt = now }],
        };
    }

    public bool TilfoejTrin(string key, DateTime at, Part part, string tekst, string citat, ForloebLimits limits, DateTime now)
    {
        if (!HarNoegle(key) || Lukket || Trin.Count >= limits.MaxTrin) return false;
        Trin.Add(new Trin { At = at, Part = part, Tekst = tekst.Trim(), Citat = citat.Trim(), CreatedAt = now });
        if (at < StartedAt) StartedAt = at;
        UpdatedAt = now;
        return true;
    }

    /// <summary>The first step stays: a forløb always has at least one.</summary>
    public bool SletTrin(string key, int trinId, DateTime now)
    {
        if (!HarNoegle(key) || Lukket || Trin.Count <= 1) return false;
        var t = Trin.FirstOrDefault(x => x.Id == trinId);
        if (t is null) return false;
        Trin.Remove(t);
        UpdatedAt = now;
        return true;
    }

    public bool KanTilfoejeDokument(string key, long size, ForloebLimits limits) =>
        HarNoegle(key) && !Lukket && size <= limits.MaxFileBytes && Dokumenter.Count < limits.MaxDokumenter;

    public Dokument TilfoejDokument(string fileName, string storedName, string contentType, long size, DateTime now)
    {
        var d = new Dokument { FileName = fileName, StoredName = storedName, ContentType = contentType, Size = size, CreatedAt = now };
        Dokumenter.Add(d);
        UpdatedAt = now;
        return d;
    }

    /// <summary>Closing again only changes the text; the close date stays the first one.</summary>
    public bool Afslut(string key, Udfald udfald, string udfaldTekst, string laering, bool delt, DateTime now)
    {
        if (!HarNoegle(key)) return false;
        Udfald = udfald;
        UdfaldTekst = udfaldTekst.Trim();
        Laering = laering.Trim();
        Delt = delt;
        ClosedAt ??= now;
        UpdatedAt = now;
        return true;
    }

    public bool SaetDelt(string key, bool delt, DateTime now)
    {
        if (!HarNoegle(key)) return false;
        Delt = delt;
        UpdatedAt = now;
        return true;
    }

    public Analyse Analyse => ForloebRules.AnalyseAf(AnalyseJson);

    public void GemAnalyse(Analyse a)
    {
        AnalyseJson = JsonSerializer.Serialize(a);
        Resume = a.Resume;
    }
}

/// <summary>Limits per forløb (config Forloebet:MaxFileBytes, MaxDocsPerForloeb, MaxTrinPerForloeb).</summary>
public sealed record ForloebLimits(long MaxFileBytes = 8L * 1024 * 1024, int MaxDokumenter = 20, int MaxTrin = 40);

public static class ForloebRules
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public static Part ParsePart(string? s) => (s ?? "").Trim().ToLowerInvariant() switch
    {
        "modpart" => Part.Modpart,
        "anden" => Part.Anden,
        _ => Part.Borger,
    };

    public static string UdfaldTekst(Udfald u) => u switch
    {
        Udfald.Aabent => "åbent",
        Udfald.Frafaldet => "frafaldet",
        Udfald.Betalt => "betalt",
        Udfald.Delvist => "delvist",
        Udfald.Afvist => "afvist",
        _ => "andet",
    };

    public static Analyse AnalyseAf(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new Analyse();
        try { return JsonSerializer.Deserialize<Analyse>(json, Json) ?? new Analyse(); }
        catch (JsonException) { return new Analyse(); }
    }

    /// <summary>The front-page headline is only rewritten (one AI call) when this changes.</summary>
    public static string OverskriftHash(IEnumerable<Forloeb> delte) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Join("|", delte.Select(x => $"{x.Id}:{x.Udfald}:{x.ClosedAt:d}:{x.Titel}")))));

    /// <summary>A suggested date from the AI ("2026-09-17"), or now when it has none.</summary>
    public static DateTime DatoEllerNu(string? dato, DateTime now) =>
        DateTime.TryParse(dato, out var dt) ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : now;
}

/// <summary>What kind of file a document is - decides how the AI gets to read it.</summary>
public static class DokumentArt
{
    public static bool IsImage(string contentType) => contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    public static bool IsPdf(string contentType, string name) => contentType == "application/pdf" || name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
    public static bool IsText(string contentType, string name) =>
        contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
        || contentType == "message/rfc822"
        || name.EndsWith(".eml", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
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

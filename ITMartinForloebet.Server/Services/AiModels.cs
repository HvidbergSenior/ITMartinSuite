using System.Text.Json.Serialization;

namespace ITMartinForloebet.Server.Services;

/// <summary>What Claude made of one informal message.</summary>
public sealed class Omskrivning
{
    public string Tekst { get; set; } = "";
    public string Part { get; set; } = "Borger";     // Borger | Modpart | Anden
    public string? Dato { get; set; }                // yyyy-MM-dd if the text names one
    public string Citat { get; set; } = "";
    public string Fjernet { get; set; } = "";        // what was left out and why, shown to the user
    // Only filled for the first message of a new forløb:
    public string Titel { get; set; } = "";
    public string Modpart { get; set; } = "";
    public string Omraade { get; set; } = "";
}

public sealed class TrinForslag
{
    public string Dato { get; set; } = "";
    public string Part { get; set; } = "Modpart";
    public string Tekst { get; set; } = "";
    public string Citat { get; set; } = "";
}

public sealed class Udkast
{
    public string Titel { get; set; } = "";
    public string Tekst { get; set; } = "";
}

public sealed class Lignende
{
    public string Slug { get; set; } = "";
    public string Linje { get; set; } = "";
}

/// <summary>The six questions. Stored as JSON on the forløb.</summary>
public sealed class Analyse
{
    public string HvadHandledeDetOm { get; set; } = "";
    public string HvadSkulleManGoere { get; set; } = "";
    public string HvadKraeves { get; set; } = "";
    public string JaOgNejTil { get; set; } = "";
    public string UnoedigTid { get; set; } = "";
    public string Love { get; set; } = "";
    public List<Udkast> Udkast { get; set; } = [];
    public List<Lignende> Lignende { get; set; } = [];
    public string Resume { get; set; } = "";

    [JsonIgnore] public bool Tom => string.IsNullOrWhiteSpace(HvadHandledeDetOm);
}

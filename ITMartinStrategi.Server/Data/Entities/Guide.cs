namespace ITMartinStrategi.Server.Data.Entities;

/// <summary>One guide card. Body is light markdown: "## heading", "- bullet", "**bold**".</summary>
public sealed class Guide
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public Game Game { get; set; } = null!;
    public string Section { get; set; } = Sections.Mekanik;
    public string Title { get; set; } = "";
    // Only for "Hvis du vælger ...": the choice it is about ("Oligarki").
    public string Trigger { get; set; } = "";
    public string Body { get; set; } = "";
    // "AI", "Martin" or "AI + Martin" once Martin has edited an AI draft.
    public string Source { get; set; } = "Martin";
    public bool Checked { get; set; }
    public string SourceUrls { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public static class Sections
{
    public const string Mekanik = "mekanik";
    public const string Start = "start";
    public const string Valg = "valg";
    public const string Mine = "mine";

    public static readonly string[] All = [Mekanik, Start, Valg, Mine];

    public static string Label(string s) => s switch
    {
        Mekanik => "Sådan virker det",
        Start => "Åbning",
        Valg => "Hvis du vælger …",
        Mine => "Mine strategier",
        _ => s,
    };

    public static string Hint(string s) => s switch
    {
        Mekanik => "Enkle forklaringer af spillets mekanikker.",
        Start => "Hvad du gør først: de første træk, ture eller runder.",
        Valg => "Når du har valgt noget – en regeringsform, en leder, en strategi – hvad gør du så?",
        Mine => "Dine egne strategier og erfaringer.",
        _ => "",
    };
}

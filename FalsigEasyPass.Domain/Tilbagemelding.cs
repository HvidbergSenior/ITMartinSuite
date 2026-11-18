namespace FalsigEasyPass.Domain;

/// <summary>Fejl = a visitor says something is wrong. Ejer = the place says "det er vores sted". Godt = "Det passede" after a visit.</summary>
public enum TilbagemeldingType { Fejl, Ejer, Godt }

/// <summary>A message about one place. Only the text and an optional mail - no name, no needs, no tracking.</summary>
public sealed class Tilbagemelding
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public string StedId { get; set; } = "";
    public TilbagemeldingType Type { get; set; }
    public string Tekst { get; set; } = "";
    public string? Mail { get; set; }
    public DateTime Tid { get; set; }
    public bool Klaret { get; set; }

    public static string? Problem(string tekst, string? mail, TilbagemeldingType type)
    {
        if (type == TilbagemeldingType.Godt) return null;
        if (string.IsNullOrWhiteSpace(tekst) || tekst.Trim().Length < 3) return "Skriv kort, hvad der er forkert.";
        if (tekst.Length > 1000) return "Højst 1000 tegn.";
        if (type == TilbagemeldingType.Ejer && !Ligner(mail)) return "Skriv stedets mail, så vi kan svare jer.";
        if (!string.IsNullOrWhiteSpace(mail) && !Ligner(mail)) return "Mailen ser forkert ud.";
        return null;
    }

    private static bool Ligner(string? s) =>
        s is { Length: > 5 and < 200 } && System.Text.RegularExpressions.Regex.IsMatch(s.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
}

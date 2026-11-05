namespace ITMartinHjem.Domain;

// "Skriv til mig" opens a Snak room (user 2026-11-05: off Facebook - people reach me through ITMartin, all talk in one place).
// The visitor gives a name and the first message; Snak makes the room and the visitor goes straight into it.
public static class SnakStartRules
{
    public const int MaxName = 60;
    public const int MaxText = 2000;
    /// <summary>Rooms one address may open per day - stops a script filling Snak, never a real person.</summary>
    public const int PerDay = 5;

    /// <returns>A Danish line for the visitor, or null when the name and message are fine.</returns>
    public static string? Check(string name, string text)
    {
        if (name.Length == 0) return "Skriv dit navn, så jeg ved, hvem du er.";
        if (name.Length > MaxName) return $"Navnet må højst være {MaxName} tegn.";
        if (text.Length == 0) return "Skriv din besked.";
        if (text.Length > MaxText) return $"Beskeden må højst være {MaxText} tegn. Skriv resten inde i rummet.";
        return null;
    }
}

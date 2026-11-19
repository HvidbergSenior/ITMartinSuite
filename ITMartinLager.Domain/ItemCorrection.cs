namespace ITMartinLager.Domain;

/// <summary>What the AI says a thing is after a person typed part of it (user 2026-11-19: "it is uncertain and i change text
/// or write something. ai need to find the correct one"). Empty text / null year = the AI does not know that part.</summary>
public sealed record Identified(string Kind, string Title, string Artist, string Series, string Number, int? Year, string Note);

/// <summary>One field where the AI's answer is not what the item has now.</summary>
public sealed record FieldChange(string Label, string Mine, string Ai);

/// <summary>Compares the AI's answer with the item and applies it on "Brug forslaget". Only what the AI is sure of counts:
/// an empty answer never wipes what the person wrote.</summary>
public static class ItemCorrection
{
    public static List<FieldChange> Changes(Item item, Identified ai)
    {
        var list = new List<FieldChange>();
        void Add(string label, string mine, string theirs)
        {
            if (theirs.Trim() != "" && !string.Equals(mine.Trim(), theirs.Trim(), StringComparison.OrdinalIgnoreCase))
                list.Add(new FieldChange(label, mine.Trim(), theirs.Trim()));
        }
        Add("Type", item.Kind, ai.Kind);
        Add("Titel", item.Title, ai.Title);
        Add("Kunstner / forfatter", item.Artist, ai.Artist);
        Add("Serie / blad", item.Series, ai.Series);
        Add("Nummer", item.Number, ai.Number);
        if (ai.Year is { } y && y != item.Year) list.Add(new FieldChange("År", item.Year?.ToString() ?? "", y.ToString()));
        return list;
    }

    public static void Apply(Item item, Identified ai)
    {
        static string Pick(string mine, string theirs) => theirs.Trim() != "" ? theirs.Trim() : mine;
        if (ai.Kind.Trim() != "") item.Kind = Kinds.Normalise(ai.Kind);
        item.Title = Pick(item.Title, ai.Title);
        item.Artist = Pick(item.Artist, ai.Artist);
        item.Series = Pick(item.Series, ai.Series);
        item.Number = Pick(item.Number, ai.Number);
        if (ai.Year is { } y) item.Year = y;
    }

    /// <summary>What the person has written so far, as the AI's starting point.</summary>
    public static string Typed(Item item) => string.Join("\n", new[]
    {
        ("Type", item.Kind), ("Titel", item.Title), ("Kunstner/forfatter", item.Artist), ("Serie/blad", item.Series),
        ("Nummer", item.Number), ("År", item.Year?.ToString() ?? ""), ("Note", item.Note),
    }.Where(f => f.Item2.Trim() != "").Select(f => $"{f.Item1}: {f.Item2.Trim()}"));
}

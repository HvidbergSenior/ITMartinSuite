namespace ITMartinAdhd.Domain.Entities;

// One thing in the home and where it was last put. Created and moved only through the methods below,
// so name and place are always filled in and within the database limits.
public sealed class StoredItem
{
    public const int MaxName = 200, MaxLocation = 500, MaxNotes = 1000;

    public int Id { get; private set; }
    public string Name { get; private set; } = "";
    public string Location { get; private set; } = "";
    public string? Notes { get; private set; }
    public string? PhotoPath { get; private set; }   // file name in the photo folder
    public DateTime StoredAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private StoredItem() { }   // EF

    // null = name or place missing.
    public static StoredItem? Create(string? name, string? location, string? notes, string? photoFile, DateTime now)
    {
        name = Clip(name, MaxName);
        location = Clip(location, MaxLocation);
        if (name.Length == 0 || location.Length == 0) return null;
        var n = Clip(notes, MaxNotes);
        return new StoredItem
        {
            Name = name, Location = location, Notes = n.Length == 0 ? null : n,
            PhotoPath = photoFile, StoredAt = now, UpdatedAt = now,
        };
    }

    public bool MoveTo(string? location, DateTime now)
    {
        location = Clip(location, MaxLocation);
        if (location.Length == 0) return false;
        Location = location;
        UpdatedAt = now;
        return true;
    }

    // What a search word matches: the thing, the place or the notes, any case.
    public bool Matches(string word) =>
        Name.Contains(word, StringComparison.OrdinalIgnoreCase)
        || Location.Contains(word, StringComparison.OrdinalIgnoreCase)
        || (Notes?.Contains(word, StringComparison.OrdinalIgnoreCase) ?? false);

    private static string Clip(string? s, int max) { s = (s ?? "").Trim(); return s.Length > max ? s[..max] : s; }
}

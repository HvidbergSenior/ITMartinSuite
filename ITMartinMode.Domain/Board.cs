namespace ITMartinMode.Domain;

/// <summary>The six points of the star ("Stjernespidserne", De 6 HV from Acceleratorkurset).</summary>
public enum StarPoint { Hvad, Hvorfor, Hvem, Hvor, Hvornaar, Hvordan }

/// <summary>IdeaOf = whose idea the note was written for (the game's idea owner at the time) - so the owner can take the notes home.</summary>
public sealed record Note(string Id, StarPoint Point, string Text, string Author, DateTimeOffset At, string IdeaOf = "");

/// <summary>The shared star board of one room: an idea in the middle, notes per point. Rules live here.</summary>
public sealed class Board
{
    public const int MaxTextLength = 300;
    public const int MaxNotesPerPoint = 150;

    private readonly List<Note> _notes = new();
    public string Idea { get; private set; } = "";
    public IReadOnlyList<Note> Notes => _notes;

    public Board() { }
    public Board(string? idea, IEnumerable<Note> notes) { Idea = idea ?? ""; _notes.AddRange(notes); }

    public void SetIdea(string? idea) => Idea = Clean(idea, 120);

    public Note Add(StarPoint point, string? text, string? author, DateTimeOffset now, string? ideaOf = null)
    {
        var t = Clean(text, MaxTextLength);
        if (t.Length == 0) throw new DomainException("Skriv lidt tekst først.");
        if (_notes.Count(n => n.Point == point) >= MaxNotesPerPoint) throw new DomainException("Der er ikke plads til flere sedler her.");
        var note = new Note(Guid.NewGuid().ToString("N")[..10], point, t, Clean(author, 40), now, Clean(ideaOf, 40));
        _notes.Add(note);
        return note;
    }

    public bool Remove(string id) => _notes.RemoveAll(n => n.Id == id) > 0;

    public void Clear() { _notes.Clear(); Idea = ""; }

    private static string Clean(string? s, int max)
    {
        var t = (s ?? "").Replace("\r", "").Trim();
        return t.Length > max ? t[..max] : t;
    }
}

public sealed class DomainException(string message) : Exception(message);

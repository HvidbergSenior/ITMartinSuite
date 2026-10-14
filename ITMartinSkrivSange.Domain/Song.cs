namespace ITMartinSkrivSange.Domain;

// The structure to begin with (user 2026-10-14: "Intro, Vers 1, Vers 2, Bridge, Vers 3, Outro - just that structure").
public enum PartKind { Intro, Vers1, Vers2, Bridge, Vers3, Outro }

public static class Parts
{
    public static readonly IReadOnlyList<PartKind> Order = Enum.GetValues<PartKind>();

    public static string Label(PartKind kind) => kind switch
    {
        PartKind.Vers1 => "Vers 1",
        PartKind.Vers2 => "Vers 2",
        PartKind.Vers3 => "Vers 3",
        var k => k.ToString(),
    };

    public static bool IsVerse(PartKind kind) => kind is PartKind.Vers1 or PartKind.Vers2 or PartKind.Vers3;

    /// <summary>The part in a recording's file name (take-vers1-...), letters and digits only.</summary>
    public static string Slug(PartKind kind) => kind.ToString().ToLowerInvariant();
}

/// <param name="ProgressionId">Which library progression the chords came from, or null when they were made from one (the outro).</param>
/// <param name="Notes">Lyric notes for the part - lines, words, ideas (user: "at the same time write lyric notes per part").</param>
public sealed record SongPart(PartKind Kind, IReadOnlyList<string> Chords, string? ProgressionId, string Notes)
{
    public string Label => Parts.Label(Kind);
    public bool HasChords => Chords.Count > 0;
    public bool HasNotes => Notes.Trim().Length > 0;
}

public sealed class SongException(string message) : Exception(message);

// A song being written: title, mood, capo, tempo, strum, and the six parts.
public sealed class Song
{
    public const int MaxTitle = 80, MaxNotes = 4000, MaxChords = 8, MinBpm = 40, MaxBpm = 200, DefaultBpm = 90;

    private readonly List<SongPart> _parts;

    public Guid Id { get; }
    public string Title { get; private set; }
    public string Mood { get; private set; }
    public int Capo { get; private set; }
    public int Bpm { get; private set; }
    public string StrumId { get; private set; }
    public DateTime CreatedAt { get; }
    public DateTime UpdatedAt { get; private set; }
    public IReadOnlyList<SongPart> Parts => _parts;

    private Song(Guid id, string title, string mood, int capo, int bpm, string strumId, IEnumerable<SongPart> parts, DateTime createdAt, DateTime updatedAt)
    {
        (Id, Title, Mood, Capo, Bpm, StrumId, CreatedAt, UpdatedAt) = (id, title, mood, capo, bpm, strumId, createdAt, updatedAt);
        // Always exactly the six parts in order, whatever was stored.
        var stored = parts.ToDictionary(p => p.Kind);
        _parts = Domain.Parts.Order.Select(k => stored.TryGetValue(k, out var p) ? p : Empty(k)).ToList();
    }

    public static Song Create(string title, DateTime nowUtc) =>
        new(Guid.NewGuid(), CleanTitle(title), ProgressionLibrary.Moods[0], 0, DefaultBpm, Strums.Default.Id, [], nowUtc, nowUtc);

    /// <summary>Rebuilds a stored song. Values outside the rules are pulled back inside instead of failing.</summary>
    public static Song Restore(Guid id, string title, string mood, int capo, int bpm, string strumId, IEnumerable<SongPart> parts, DateTime createdAt, DateTime updatedAt) =>
        new(id, string.IsNullOrWhiteSpace(title) ? "Uden titel" : title.Trim(),
            ProgressionLibrary.Moods.Contains(mood) ? mood : ProgressionLibrary.Moods[0],
            Math.Clamp(capo, 0, Domain.Capo.Max), Math.Clamp(bpm, MinBpm, MaxBpm), Strums.Find(strumId).Id, parts, createdAt, updatedAt);

    public SongPart Part(PartKind kind) => _parts[(int)kind];

    /// <summary>The folder the song's practice takes go in (recordings/sang-...).</summary>
    public string RecordingKey => "sang-" + Id.ToString("N");

    public void Rename(string title, DateTime nowUtc) { Title = CleanTitle(title); Touch(nowUtc); }

    public void SetMood(string mood, DateTime nowUtc)
    {
        if (!ProgressionLibrary.Moods.Contains(mood)) throw new SongException($"Ukendt stemning: {mood}");
        Mood = mood; Touch(nowUtc);
    }

    public void SetCapo(int capo, DateTime nowUtc)
    {
        if (capo is < 0 or > Domain.Capo.Max) throw new SongException($"Capo skal være 0-{Domain.Capo.Max}.");
        Capo = capo; Touch(nowUtc);
    }

    public void SetBpm(int bpm, DateTime nowUtc) { Bpm = Math.Clamp(bpm, MinBpm, MaxBpm); Touch(nowUtc); }

    public void SetStrum(string strumId, DateTime nowUtc) { StrumId = Strums.Find(strumId).Id; Touch(nowUtc); }

    public void UseProgression(PartKind kind, Progression p, DateTime nowUtc) => UseChords(kind, p.Chords, p.Id, nowUtc);

    /// <summary>Only open grips (the capo does the key), 1-8 chords.</summary>
    public void UseChords(PartKind kind, IReadOnlyList<string> chords, string? progressionId, DateTime nowUtc)
    {
        if (chords.Count is 0 or > MaxChords) throw new SongException($"En del skal have 1-{MaxChords} akkorder.");
        if (chords.FirstOrDefault(c => !Chords.IsOpenGrip(c)) is { } bad) throw new SongException($"{bad} er ikke et åbent greb - brug capo i stedet.");
        _parts[(int)kind] = Part(kind) with { Chords = chords.ToList(), ProgressionId = progressionId };
        Touch(nowUtc);
    }

    /// <summary>Swaps one chord. The part is then its own (no longer a library progression).</summary>
    public void ReplaceChord(PartKind kind, int index, string chord, DateTime nowUtc)
    {
        var chords = Part(kind).Chords.ToList();
        if (index < 0 || index >= chords.Count) throw new SongException("Den akkord findes ikke i delen.");
        chords[index] = chord;
        UseChords(kind, chords, null, nowUtc);
    }

    /// <summary>Puts a chord in right after <paramref name="index"/>.</summary>
    public void InsertChordAfter(PartKind kind, int index, string chord, DateTime nowUtc)
    {
        var chords = Part(kind).Chords.ToList();
        if (index < -1 || index >= chords.Count) throw new SongException("Den akkord findes ikke i delen.");
        chords.Insert(index + 1, chord);
        UseChords(kind, chords, null, nowUtc);
    }

    public void RemoveChord(PartKind kind, int index, DateTime nowUtc)
    {
        var chords = Part(kind).Chords.ToList();
        if (index < 0 || index >= chords.Count) throw new SongException("Den akkord findes ikke i delen.");
        chords.RemoveAt(index);
        if (chords.Count == 0) ClearChords(kind, nowUtc); else UseChords(kind, chords, null, nowUtc);
    }

    public void ClearChords(PartKind kind, DateTime nowUtc)
    {
        _parts[(int)kind] = Part(kind) with { Chords = [], ProgressionId = null };
        Touch(nowUtc);
    }

    public void WriteNotes(PartKind kind, string? notes, DateTime nowUtc)
    {
        // Kept as typed (no trim): it is saved while typing, and a trimmed save would eat the space before the next word.
        var n = (notes ?? "").Replace("\r\n", "\n");
        if (n.Length > MaxNotes) n = n[..MaxNotes];
        if (n == Part(kind).Notes) return;
        _parts[(int)kind] = Part(kind) with { Notes = n };
        Touch(nowUtc);
    }

    /// <summary>The verse the others follow: the first verse with chords (not counting <paramref name="except"/>).</summary>
    public SongPart? LeadVerse(PartKind? except = null) =>
        _parts.FirstOrDefault(p => Domain.Parts.IsVerse(p.Kind) && p.Kind != except && p.HasChords);

    /// <summary>The grip the song comes home to - the lead verse's key, else the first part with chords. Null before any chords.</summary>
    public string? HomeGrip
    {
        get
        {
            var part = LeadVerse() ?? _parts.FirstOrDefault(p => p.HasChords);
            if (part is null) return null;
            return ProgressionLibrary.Find(part.ProgressionId)?.Home ?? part.Chords[0];
        }
    }

    /// <summary>The key it sounds in with the capo on.</summary>
    public string? SoundingKey => HomeGrip is { } h ? Domain.Capo.SoundingKey(h, Capo) : null;

    /// <summary>One line on top of the page: key, capo, tempo, how far the song is.</summary>
    public string Summary()
    {
        var bits = new List<string>();
        if (HomeGrip is { } home)
            bits.Add(Capo == 0 ? $"Toneart {Chords.KeyName(home)}" : $"Klinger i {SoundingKey} (capo {Capo}, greb i {Chords.KeyName(home)})");
        bits.Add($"{Bpm} BPM");
        bits.Add($"{_parts.Count(p => p.HasChords)} af 6 dele har akkorder");
        bits.Add($"{_parts.Count(p => p.HasNotes)} har tekstnoter");
        return string.Join(" · ", bits);
    }

    private void Touch(DateTime nowUtc) => UpdatedAt = nowUtc;

    private static SongPart Empty(PartKind kind) => new(kind, [], null, "");

    private static string CleanTitle(string? title)
    {
        var t = (title ?? "").Trim();
        if (t.Length == 0) throw new SongException("Sangen skal have en titel.");
        return t.Length > MaxTitle ? t[..MaxTitle] : t;
    }
}

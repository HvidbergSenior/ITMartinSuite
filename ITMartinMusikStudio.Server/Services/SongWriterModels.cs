namespace ITMartinMusikStudio.Server.Services;

// Ported from ITMartinSongCreator (undeployed prototype) directly into
// MusikStudio's own /songwriter flow, rather than running SongCreator as a
// separate app - same rules-based logic (no AI calls), just folded into the
// app Martin actually uses.
public sealed record MelodyIdea(string Title, string Description, string? TargetChord = null);

public sealed class SongStructure
{
    public string Name { get; init; } = "";
    public List<string> Sections { get; init; } = [];
    public Dictionary<string, string> Suggestions { get; init; } = [];
    public Dictionary<string, Func<List<string>, List<string>>> SectionVariations { get; init; } = [];
}

public sealed record PracticeStep(string Section, List<string> Chords, string? Note);

public static class ChordTransposer
{
    private static readonly string[] Notes = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];

    public static List<string> Transpose(List<string> chords, int semitones)
    {
        var result = new List<string>();
        foreach (var chord in chords)
        {
            var note = chord.Length > 1 && (chord[1] == '#' || chord[1] == 'b')
                ? chord[..2]
                : chord[..1];
            var suffix = chord[note.Length..];

            var normalized = note switch
            {
                "Db" => "C#", "Eb" => "D#", "Gb" => "F#", "Ab" => "G#", "Bb" => "A#",
                _ => note
            };

            var index = Array.IndexOf(Notes, normalized);
            if (index == -1) { result.Add(chord); continue; }

            var newIndex = ((index + semitones) % Notes.Length + Notes.Length) % Notes.Length;
            result.Add(Notes[newIndex] + suffix);
        }
        return result;
    }
}

public static class ProgressionVariations
{
    public static List<string> Invert(List<string> chords)
    {
        var list = new List<string>(chords);
        list.Reverse();
        return list;
    }

    public static List<string> DropLast(List<string> chords) =>
        chords.Count <= 1 ? chords : chords.Take(chords.Count - 1).ToList();

    public static List<string> AddPassingChord(List<string> chords, string passingChord)
    {
        var result = new List<string>();
        foreach (var chord in chords) { result.Add(chord); result.Add(passingChord); }
        return result;
    }

    public static List<string> LiftChords(List<string> chords, int semitones) =>
        ChordTransposer.Transpose(chords, semitones);
}

public static class MelodyIdeas
{
    public static List<MelodyIdea> GetIdeas(List<string> chords)
    {
        var ideas = new List<MelodyIdea>();
        foreach (var chord in chords)
        {
            ideas.Add(new MelodyIdea($"Løft en finger i {chord}", $"Prøv at løfte en finger let i {chord} for at skabe variation", chord));
            ideas.Add(new MelodyIdea($"Hammer-on i {chord}", $"Tilføj et hammer-on mens du spiller {chord}", chord));
        }
        ideas.Add(new MelodyIdea("Fyld på diskantstreng", "Spil den høje E-streng mellem picking-noderne"));
        ideas.Add(new MelodyIdea("Pause for groove", "Spring en node over ind imellem for at skabe rytmisk variation"));
        return ideas;
    }
}

public static class SongStructures
{
    public static List<SongStructure> GetStructures() =>
    [
        new SongStructure
        {
            Name = "Pop-struktur",
            Sections = ["Intro", "Vers", "Omkvæd", "Bro", "Outro"],
            Suggestions = new()
            {
                ["Intro"] = "Blødt picking",
                ["Vers"] = "Hold det simpelt",
                ["Omkvæd"] = "Stryg kraftigere",
                ["Bro"] = "Tilføj spænding",
                ["Outro"] = "Slut blødt",
            },
            SectionVariations = new()
            {
                ["Vers"] = ProgressionVariations.DropLast,
                ["Omkvæd"] = chords => ProgressionVariations.LiftChords(chords, 2),
                ["Bro"] = ProgressionVariations.Invert,
            },
        },
        new SongStructure
        {
            Name = "Ballade",
            Sections = ["Intro", "Vers", "Omkvæd", "Vers", "Omkvæd", "Bro", "Omkvæd", "Outro"],
            Suggestions = new()
            {
                ["Intro"] = "Blødt fingerpicking, langsomt tempo",
                ["Vers"] = "Hold akkorderne simple",
                ["Omkvæd"] = "Stryg blidt men kraftigere end verset",
                ["Bro"] = "Tilføj en mol-variation",
                ["Outro"] = "Langsom udtoning",
            },
            SectionVariations = new()
            {
                ["Vers"] = chords => ProgressionVariations.AddPassingChord(chords, "D"),
                ["Omkvæd"] = chords => ProgressionVariations.LiftChords(chords, 1),
                ["Bro"] = ProgressionVariations.Invert,
            },
        },
    ];

    public static List<PracticeStep> GeneratePracticeSteps(List<string> chords, SongStructure structure) =>
        structure.Sections
            .Select(section =>
            {
                var sectionChords = structure.SectionVariations.TryGetValue(section, out var variation)
                    ? variation(chords)
                    : chords;
                structure.Suggestions.TryGetValue(section, out var note);
                return new PracticeStep(section, sectionChords, note);
            })
            .ToList();
}

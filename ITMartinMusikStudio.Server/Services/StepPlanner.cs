using ITMartinMusikStudio.Server.Data.Entities;

namespace ITMartinMusikStudio.Server.Services;

public enum StepStatus { NotStarted, Done, NotNecessary }

// TargetTab/TargetAnchor drive "take me there" navigation from the
// checklist - TargetTab is a Studio.razor tab key ("forbered"), null when
// the step lives on a single-page flow (SongWriter.razor) that only needs
// to scroll. TargetAnchor is an element id to scroll into view.
public sealed record SongStep(string Key, string Label, StepStatus Status, string? TargetTab, string? TargetAnchor);

public enum SongFlow { Recording, Writing } // "Indøv sang" vs "Skriv sang"

// Pure rules over a StudioSong - no DI, same idiom as SongWriterModels.cs.
// Step completion is inferred from the entity's own fields wherever
// possible; SkippedSteps is the only bit of state that can't be inferred.
public static class StepPlanner
{
    public static SongFlow GetFlow(StudioSong s) =>
        string.IsNullOrWhiteSpace(s.SourceFile) && string.IsNullOrWhiteSpace(s.SpotifyTrackId)
            ? SongFlow.Writing
            : SongFlow.Recording;

    public static HashSet<string> Skipped(StudioSong s) =>
        s.SkippedSteps.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();

    public static string ToggleNotNecessary(StudioSong s, string stepKey)
    {
        var set = Skipped(s);
        if (!set.Remove(stepKey)) set.Add(stepKey);
        return string.Join(",", set);
    }

    // hasSketches/hasPublishedVersion are filesystem-backed facts StepPlanner
    // can't look up itself (it's a pure rules class, no DI) - callers already
    // have a StudioLibraryService in scope and pass the booleans in, same as
    // any other precomputed input.
    public static List<SongStep> GetSteps(StudioSong s, bool hasSketches, bool hasPublishedVersion = false) =>
        GetFlow(s) == SongFlow.Recording ? RecordingSteps(s, hasPublishedVersion) : WritingSteps(s, hasSketches);

    // First not-started step, in the deliberate order the lists below are
    // written in - used to point the user at "what's next" instead of
    // leaving them to scan a flat checklist for gaps.
    public static SongStep? NextStep(List<SongStep> steps) =>
        steps.FirstOrDefault(x => x.Status == StepStatus.NotStarted);

    // "version" (a published take) can only ever become true AFTER
    // recording, so it must never gate entry into the Indspil tab - excluded
    // here, but still shown in the display checklist.
    public static bool IsReadyToRecord(StudioSong s, bool hasSketches) =>
        GetSteps(s, hasSketches).Where(x => x.Key != "version").All(x => x.Status != StepStatus.NotStarted);

    private static StepStatus Status(bool hasData, HashSet<string> skipped, string key) =>
        hasData ? StepStatus.Done : skipped.Contains(key) ? StepStatus.NotNecessary : StepStatus.NotStarted;

    private static List<SongStep> RecordingSteps(StudioSong s, bool hasPublishedVersion)
    {
        var skipped = Skipped(s);
        var hasPattern = !string.IsNullOrWhiteSpace(s.StrumPattern) || !string.IsNullOrWhiteSpace(s.FingerpickPattern);
        return
        [
            // Navn is always Done - every song has a non-empty Title by
            // construction at creation, so it can never be NotStarted, and
            // "title not necessary" makes no sense as an override.
            new("navn", "Navn", StepStatus.Done, null, null),
            new("kilde", "Kildefil",
                Status(!string.IsNullOrWhiteSpace(s.SourceFile) || !string.IsNullOrWhiteSpace(s.SpotifyTrackId), skipped, "kilde"),
                "forbered", "card-kilde"),
            new("akkorder", "Akkorder",
                Status(!string.IsNullOrWhiteSpace(s.ChordChart), skipped, "akkorder"),
                "forbered", "card-akkorder"),
            new("tekst", "Tekst",
                Status(!string.IsNullOrWhiteSpace(s.Lyrics), skipped, "tekst"),
                "forbered", "card-tekst"),
            new("spillemaade", "Spillemåde",
                Status(hasPattern, skipped, "spillemaade"),
                "forbered", "card-monstre"),
            new("version", "Indspillet version",
                Status(hasPublishedVersion, skipped, "version"),
                "optag", null),
        ];
    }

    private static List<SongStep> WritingSteps(StudioSong s, bool hasSketches)
    {
        var skipped = Skipped(s);
        var hasPattern = !string.IsNullOrWhiteSpace(s.StrumPattern) || !string.IsNullOrWhiteSpace(s.FingerpickPattern);
        return
        [
            new("navn", "Navn", StepStatus.Done, null, null),
            new("akkorder", "Akkordrække", Status(!string.IsNullOrWhiteSpace(s.ChordChart), skipped, "akkorder"), null, "sw-progression"),
            new("spillemaade", "Spillemønster", Status(hasPattern, skipped, "spillemaade"), null, "sw-pattern"),
            new("tempo", "Tempo", Status(s.Tempo.HasValue, skipped, "tempo"), null, "sw-tempo"),
            new("sketch", "Idé-optagelse", Status(hasSketches, skipped, "sketch"), null, "sw-sketch"),
            new("tekst", "Tekstudkast", Status(!string.IsNullOrWhiteSpace(s.Lyrics), skipped, "tekst"), null, "sw-lyrics"),
        ];
    }
}

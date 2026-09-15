using ITMartinMusikStudio.Server.Data.Entities;

namespace ITMartinMusikStudio.Server.Services;

public enum StepStatus { NotStarted, Done, NotNecessary }

// TargetTab/TargetAnchor drive "take me there" navigation from the
// checklist - TargetTab is a Studio.razor phase key ("forbered" = Trin,
// "optag" = Indspil), TargetAnchor an element id to scroll into view.
public sealed record SongStep(string Key, string Label, StepStatus Status, string? TargetTab, string? TargetAnchor);

// Pure rules over a StudioSong - no DI. Step completion is inferred from
// the entity's own fields wherever possible; SkippedSteps is the only bit
// of state that can't be inferred. Writing a song from scratch lives in the
// SkrivSange app; here every song follows the same Trin -> Indspil path.
public static class StepPlanner
{
    public static HashSet<string> Skipped(StudioSong s) =>
        s.SkippedSteps.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();

    public static string ToggleNotNecessary(StudioSong s, string stepKey)
    {
        var set = Skipped(s);
        if (!set.Remove(stepKey)) set.Add(stepKey);
        return string.Join(",", set);
    }

    // hasPublishedVersion is a filesystem-backed fact StepPlanner can't look
    // up itself - callers already have a StudioLibraryService in scope and
    // pass the boolean in. The unused `hasSketches` parameter is kept so the
    // call sites read the same as before the SkrivSange split.
    public static List<SongStep> GetSteps(StudioSong s, bool hasSketches, bool hasPublishedVersion = false)
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

    // First not-started step, in the deliberate order the list above is
    // written in - used to point the user at "what's next".
    public static SongStep? NextStep(List<SongStep> steps) =>
        steps.FirstOrDefault(x => x.Status == StepStatus.NotStarted);

    // "version" (a published take) can only ever become true AFTER
    // recording, so it must never gate entry into Indspil - excluded here,
    // but still shown in the display checklist.
    public static bool IsReadyToRecord(StudioSong s, bool hasSketches) =>
        GetSteps(s, hasSketches).Where(x => x.Key != "version").All(x => x.Status != StepStatus.NotStarted);

    private static StepStatus Status(bool hasData, HashSet<string> skipped, string key) =>
        hasData ? StepStatus.Done : skipped.Contains(key) ? StepStatus.NotNecessary : StepStatus.NotStarted;
}

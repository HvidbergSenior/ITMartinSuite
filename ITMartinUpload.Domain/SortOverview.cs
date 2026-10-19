namespace ITMartinUpload.Domain;

// Overview of what happened to the customer's uploaded files (user 2026-10-19: "user need overblik which state the
// files are in"). FileSorter writes _kilder.json in the library after each run; a file uploaded after that run is still
// waiting. Uploaded files are only marked here - never moved or deleted.
public enum SortState { Waiting, Sorted, Duplicate, NotIncluded, Failed }

/// <summary>One line of FileSorter's _kilder.json: the file's path in the upload folder and where it ended up.</summary>
public sealed record SourceEntry(string Source, string State, string? LibraryPath);

public sealed record SortReport(DateTimeOffset RunUtc, IReadOnlyList<SourceEntry> Files);

public sealed record UploadedFile(string Name, DateTime ModifiedUtc);

public sealed record FileSortStatus(string Name, SortState State, string? LibraryPath);

public sealed record SortSummary(int Received, int Sorted, int Duplicates, int NotIncluded, int Failed, int Waiting, DateTimeOffset? LastRunUtc)
{
    public bool AnySorted => LastRunUtc is not null;
}

public static class SortOverview
{
    public static IReadOnlyList<FileSortStatus> Build(IEnumerable<UploadedFile> uploaded, SortReport? report)
    {
        var bySource = (report?.Files ?? [])
            .GroupBy(e => e.Source, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        return uploaded.Select(f =>
        {
            // Uploaded (or re-sent) after the last run: FileSorter has not seen this version yet.
            if (report is null || f.ModifiedUtc > report.RunUtc.UtcDateTime || !bySource.TryGetValue(f.Name, out var e))
                return new FileSortStatus(f.Name, SortState.Waiting, null);
            return new FileSortStatus(f.Name, Parse(e.State), e.LibraryPath);
        }).ToList();
    }

    public static SortSummary Summarize(IReadOnlyCollection<FileSortStatus> files, SortReport? report) => new(
        files.Count,
        files.Count(f => f.State == SortState.Sorted),
        files.Count(f => f.State == SortState.Duplicate),
        files.Count(f => f.State == SortState.NotIncluded),
        files.Count(f => f.State == SortState.Failed),
        files.Count(f => f.State == SortState.Waiting),
        report?.RunUtc);

    /// <summary>The words the customer sees.</summary>
    public static string Label(SortState s) => s switch
    {
        SortState.Sorted => "I galleriet",
        SortState.Duplicate => "Dublet – findes allerede",
        SortState.NotIncluded => "Ikke med i galleriet",
        SortState.Failed => "Kunne ikke læses",
        _ => "Venter på sortering",
    };

    private static SortState Parse(string state) => state switch
    {
        "Sorteret" => SortState.Sorted,
        "Dublet" => SortState.Duplicate,
        "IkkeMed" => SortState.NotIncluded,
        "Fejl" => SortState.Failed,
        _ => SortState.Waiting,
    };
}

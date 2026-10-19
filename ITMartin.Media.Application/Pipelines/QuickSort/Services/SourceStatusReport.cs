using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Application.Pipelines.QuickSort.Services;

// Where every file of the run's source folder ended up (user 2026-10-19: "the user needs overblik which state the files
// are in"). Saved as _kilder.json in the library; the Upload app matches it to the customer's uploaded files by path.
// Upload files are only marked, never moved or deleted.
public static class SourceStatusReport
{
    public const string FileName = "_kilder.json";

    public const string Sorted = "Sorteret";
    public const string Duplicate = "Dublet";
    public const string NotIncluded = "IkkeMed";
    public const string Failed = "Fejl";

    public sealed record Entry(string Source, string State, string? LibraryPath);

    public sealed record Report(DateTimeOffset RunUtc, List<Entry> Files);

    public static Report Build(
        string rootPath,
        string exportRoot,
        IReadOnlyCollection<MediaFile> mediaFiles,
        IReadOnlyCollection<DuplicateGroup> duplicateGroups,
        IEnumerable<string> allSourceFiles,
        DateTimeOffset runUtc)
    {
        // A duplicate is a group member that was not kept - the kept copy is the one with CleanupDecision.Keep.
        var inDuplicateGroup = duplicateGroups.SelectMany(g => g.Files).Select(f => f.Id).ToHashSet();

        string Rel(string path) => Path.GetRelativePath(rootPath, path).Replace('\\', '/');

        var entries = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in mediaFiles)
        {
            var source = Rel(m.OriginalPath);
            var state =
                m.Failed ? Failed
                : m.CleanupDecision == CleanupDecision.Delete && inDuplicateGroup.Contains(m.Id) ? Duplicate
                : m.ExportedPath is { } ? Sorted
                : NotIncluded;
            var libraryPath = state == Sorted ? Path.GetRelativePath(exportRoot, m.ExportedPath!).Replace('\\', '/') : null;
            entries[source] = new Entry(source, state, libraryPath);
        }

        // Files the media rules left out (Facebook icons, unsupported types ...) never became MediaFiles.
        foreach (var path in allSourceFiles)
        {
            var source = Rel(path);
            entries.TryAdd(source, new Entry(source, NotIncluded, null));
        }

        return new Report(runUtc, entries.Values.OrderBy(e => e.Source, StringComparer.OrdinalIgnoreCase).ToList());
    }
}

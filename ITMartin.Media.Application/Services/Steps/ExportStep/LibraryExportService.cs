using ITMartin.Media.Application.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Helpers;
using System.Linq;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Services.Steps.ExportStep;

public class LibraryExportService
    : ILibraryExportService
{
    private readonly IMediaNamingService
        _mediaNamingService;
    private readonly IAudioMetadataService
        _audioMetadataService;
    private readonly ILogger<LibraryExportService>
        _logger;
    public LibraryExportService(
        IMediaNamingService mediaNamingService, IAudioMetadataService audioMetadataService, ILogger<LibraryExportService> logger)
    {
        _mediaNamingService =
            mediaNamingService;
        _audioMetadataService =
            audioMetadataService;
        _logger = logger;
    }

    public async Task ExportAsync(
        IEnumerable<MediaFile> files,
        string root,
        Func<int, int, string, string, Task>? progress)
    {
        var list =
            files?.ToList() ?? [];

        if (!list.Any())
            return;

        if (string.IsNullOrWhiteSpace(root))
        {
            throw new Exception(
                "Export root is invalid");
        }

        EnsureBaseFolders(root);

        int total = list.Count;
        int done = 0;

        // =========================
        // PRE-PASS: group each (category, year)'s dated files into
        // date-range buckets targeting GroupTargetSize each, replacing a
        // fixed calendar-Month split entirely (a quiet January and a
        // 300-photo vacation week no longer both get exactly one folder
        // each). A year with fewer than GroupTargetSize dated files stays as
        // just one flat group - no subfolder at all.
        //
        // Busier years are split recursively at whatever the single BEST
        // (largest) gap between consecutive photos is - not a fixed "only
        // cut past N days" rule. A fixed-day threshold broke down for a year
        // with near-daily photos and no gap ever exceeding it: it produced
        // one 5,966-file "Jan-Dec" group, defeating the entire point (see
        // [[project_package1_month_split]]). Recursive best-gap bisection
        // can't do that - each split strictly shrinks both halves, so it
        // always terminates with reasonably-sized groups even when gaps are
        // small everywhere; it just uses whatever the locally best gap is,
        // however small. The search for that gap is restricted to the
        // "middle band" of each run (at least half a target-size in from
        // either end) so a split never produces a sliver group far below
        // target just to grab a slightly bigger gap near one edge.
        //
        // Duplicates/DeleteCandidates/Music/Undated/year-only files never go
        // through this grouping, so they're not counted here.
        // =========================
        const int GroupTargetSize = 50;
        const int FlatIfAtMost = GroupTargetSize;

        // Explicit lookup, not culture-dependent DateTime formatting ("MMMM"/
        // "MMM") - Package1 historically produced English month names
        // regardless of the runtime's culture/globalization settings (see
        // Gallery.Server's own DanishMonthNames translation table, which
        // exists specifically because of that), and this needs to be Danish
        // text directly, not something translated later at display time.
        var danishMonthFull = new[] { "Januar", "Februar", "Marts", "April", "Maj", "Juni", "Juli", "August", "September", "Oktober", "November", "December" };

        // Calendar-bucket split, not gap-based: a gap-driven algorithm (tried
        // first) could merge genuinely unrelated dates months apart into one
        // group just to hit the size target (e.g. "Jul-Okt" silently spanning
        // three unrelated days) - a vague, meaningless label. Fixed calendar
        // buckets never span more than a third of a year, so a label always
        // means something. Splits a year into 4-month buckets (thirds of a
        // year); any bucket over target splits into its two 2-month halves;
        // any of those still over target splits into its two 1-month halves.
        // Stops at 1 month regardless of size - matches the user's explicit
        // spec, no further (e.g. halvdel) splitting below that.
        static IEnumerable<List<MediaFile>> SplitByCalendarBuckets(List<MediaFile> sorted, int targetSize)
        {
            foreach (var quarter in sorted.GroupBy(f => (f.CreatedAt!.Value.Month - 1) / 4))
            {
                var quarterFiles = quarter.ToList();
                if (quarterFiles.Count <= targetSize) { yield return quarterFiles; continue; }

                foreach (var biMonth in quarterFiles.GroupBy(f => (f.CreatedAt!.Value.Month - 1) / 2))
                {
                    var biMonthFiles = biMonth.ToList();
                    if (biMonthFiles.Count <= targetSize) { yield return biMonthFiles; continue; }

                    foreach (var month in biMonthFiles.GroupBy(f => f.CreatedAt!.Value.Month))
                        yield return month.ToList();
                }
            }
        }

        var groupLabelByFileId = new Dictionary<Guid, string>();

        foreach (var yearGroup in list
            .Where(f => f.ExportSubFolder is not ("Duplicates" or "DeleteCandidates"))
            .Where(f => CategoryHelper.GetCategory(f) != "Musik")
            .Where(f => !f.IsYearOnly && f.IsDateReliable && f.CreatedAt.HasValue)
            .GroupBy(f => (Category: CategoryHelper.GetCategory(f), Year: Math.Max(f.Year, 2000))))
        {
            var sorted = yearGroup.OrderBy(f => f.CreatedAt!.Value).ToList();
            if (sorted.Count <= FlatIfAtMost) continue; // stays flat - no label assigned

            // SplitByCalendarBuckets yields groups in chronological order
            // (source is pre-sorted by date), so a plain 1-based counter
            // sorts correctly in Explorer without looking like a day number -
            // just a running index of "which group is this, within the
            // year," not tied to any calendar value.
            var groupIndex = 0;
            foreach (var group in SplitByCalendarBuckets(sorted, GroupTargetSize))
            {
                groupIndex++;

                // Month names only, never day numbers - and always the real
                // first/last MONTH actually present in the group, not the
                // calendar bucket's own boundary (e.g. a Jan-Apr bucket with
                // photos only in Feb-Mar reads "Februar-Marts", not
                // "Januar-April").
                var startMonth = group[0].CreatedAt!.Value.Month;
                var endMonth = group[^1].CreatedAt!.Value.Month;
                var label = startMonth == endMonth
                    ? $"{groupIndex} {danishMonthFull[startMonth - 1]}"
                    : $"{groupIndex} {danishMonthFull[startMonth - 1]}-{danishMonthFull[endMonth - 1]}";
                foreach (var f in group) groupLabelByFileId[f.Id] = label;
            }
        }

        // =========================
        // COPY FILES
        // =========================

        foreach (var file in list)
        {
            try
            {
                var category =
                    CategoryHelper
                        .GetCategory(file);

                // Music has no meaningful "date taken" the way photos do (ID3
                // Year is release year, not acquisition date), so it always
                // gets an Artist/Album folder instead of being routed through
                // the year/month or Undated logic below.
                var isMusic =
                    category == "Musik";

                var musicSubPath =
                    isMusic
                        ? Path.Combine(
                            SanitizeFolderName(
                                string.IsNullOrWhiteSpace(file.Artist) ? "Ukendt kunstner" : file.Artist),
                            SanitizeFolderName(
                                string.IsNullOrWhiteSpace(file.Album) ? "Ukendt album" : file.Album))
                        : null;

                // SAFER DATE HANDLING

                var safeMonth =
                    Math.Clamp(
                        file.Month,
                        1,
                        12);

                var safeYear =
                    Math.Max(
                        file.Year,
                        2000);

                var monthFolder =
                    $"{safeMonth:00}-{new DateTime(
                        safeYear,
                        safeMonth,
                        1).ToString("MMMM")}";

                var targetDir =
                    file.ExportSubFolder == "Duplicates"
                        ? musicSubPath is not null
                            ? Path.Combine(root, "Duplikater", category, musicSubPath)
                            : Path.Combine(
                                root,
                                "Duplikater",
                                category,
                                safeYear.ToString(),
                                monthFolder)
                        : file.ExportSubFolder == "DeleteCandidates"
                            ? Path.Combine(
                                root,
                                "SlettesKandidater",
                                category)
                            : musicSubPath is not null
                                ? Path.Combine(root, category, musicSubPath)
                                : file.IsYearOnly
                                    // Year came from an ancestor folder name, not a
                                    // real date - sort by it, but never claim a
                                    // specific month we don't actually know.
                                    ? Path.Combine(
                                        root,
                                        category,
                                        safeYear.ToString(),
                                        "Ukendt måned")
                                    : !file.IsDateReliable
                                        // No "Udaterede" catch-all - a genuinely
                                        // dateless real photo still belongs to its
                                        // real category, it just sits directly at
                                        // the category root instead of a Year
                                        // folder (see feedback_no_catchall_folders).
                                        ? Path.Combine(
                                            root,
                                            category)
                                        : groupLabelByFileId.TryGetValue(file.Id, out var groupLabel)
                                            ? Path.Combine(
                                                root,
                                                category,
                                                safeYear.ToString(),
                                                SanitizeFolderName(groupLabel))
                                            : Path.Combine(
                                                root,
                                                category,
                                                safeYear.ToString());

                Directory.CreateDirectory(
                    targetDir);

                // =========================
                // SOURCE FILE
                // =========================

                var sourcePath =
                    file.NormalizedPath ??
                    file.FullPath;
                _logger.LogInformation(
                    """
                    Export:
                    Original={Original}
                    Normalized={Normalized}
                    Using={Using}
                    """,
                    file.FullPath,
                    file.NormalizedPath,
                    file.NormalizedPath ?? file.FullPath);
                // =========================
                // AI FILE NAME
                // =========================

                var safeFileName =
                    isMusic && file.TrackNumber is > 0 && !string.IsNullOrWhiteSpace(file.Title)
                        ? $"{file.TrackNumber:00} - {SanitizeFolderName(file.Title!)}{Path.GetExtension(file.FullPath).ToLowerInvariant()}"
                        : _mediaNamingService
                            .BuildFileName(file);

                var targetPath =
                    Path.Combine(
                        targetDir,
                        safeFileName);

                // Avoid collisions

                targetPath =
                    EnsureUniqueFileName(
                        targetPath);

                // =========================
                // COPY
                // =========================

                if (!File.Exists(targetPath))
                {
                    await CopyFileAsync(
                        sourcePath,
                        targetPath);
                }

                // One cover.jpg per album folder, pulled from whichever track
                // happens to carry embedded artwork - not every track in an
                // album has it, so this isn't limited to the first file copied.
                if (isMusic && file.ExportSubFolder is not ("Duplicates" or "DeleteCandidates"))
                {
                    var coverPath =
                        Path.Combine(targetDir, "cover.jpg");

                    if (!File.Exists(coverPath))
                    {
                        var coverBytes =
                            _audioMetadataService.GetCoverArt(sourcePath);

                        if (coverBytes is { Length: > 0 })
                        {
                            await File.WriteAllBytesAsync(
                                coverPath,
                                coverBytes);
                        }
                    }
                }

                // =========================
                // STORE EXPORTED PATH
                // =========================

                file.ExportedPath =
                    targetPath;

                done++;

                if (progress != null)
                {
                    await progress(
                        done,
                        total,
                        Path.GetFileName(targetPath),
                        "Copying files...");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"🔥 EXPORT ERROR: {file.FullPath}");

                Console.WriteLine(ex);
            }
        }

        // =========================
        // DONE
        // =========================

        if (progress != null)
        {
            await progress(
                total,
                total,
                "",
                "Done ✅");
        }
    }

    private static string SanitizeFolderName(
        string name)
    {
        var invalid =
            Path.GetInvalidFileNameChars();

        var cleaned =
            new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray())
                .Trim()
                .TrimEnd('.');

        return string.IsNullOrWhiteSpace(cleaned) ? "Ukendt" : cleaned;
    }

    private static void EnsureBaseFolders(
        string exportRoot)
    {
        var baseFolders =
            new[]
            {
                "Billeder",
                "Videoer",
                "Dokumenter",
                "Musik",
                "Memes",
                "Chat",
                "Skærmbilleder",
                "LivePhotos",
                "SlettesKandidater",
                "Duplikater",
                "Ikke_identificeret"
            };

        foreach (var folder in baseFolders)
        {
            var path =
                Path.Combine(
                    exportRoot,
                    folder);

            Directory.CreateDirectory(path);
        }
    }

    private static async Task CopyFileAsync(
        string sourcePath,
        string destinationPath)
    {
        const int bufferSize = 81920;

        using var source =
            new FileStream(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize,
                useAsync: true);

        using var destination =
            new FileStream(
                destinationPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize,
                useAsync: true);

        await source.CopyToAsync(
            destination);
    }

    // Collision handling is filename-only, not content-based - if two source
    // files share a name (e.g. the same photo/video re-imported from two
    // different backups or an add-on's corrected copy sitting next to the
    // original) this keeps both as separate files (name, name_2, name_3, ...)
    // instead of deduping. Real, byte-identical duplicates can end up in the
    // exported library this way, inflating file/photo/video counts - see
    // DuplicateDetectionWorkflowStep for the actual dedup pass, which runs
    // earlier and is a separate concern from this pure naming-collision fix.
    private static string EnsureUniqueFileName(
        string path)
    {
        if (!File.Exists(path))
        {
            return path;
        }

        var directory =
            Path.GetDirectoryName(path)!;

        var name =
            Path.GetFileNameWithoutExtension(path);

        var ext =
            Path.GetExtension(path);

        var counter = 2;

        while (true)
        {
            var candidate =
                Path.Combine(
                    directory,
                    $"{name}_{counter}{ext}");

            if (!File.Exists(candidate))
            {
                return candidate;
            }

            counter++;
        }
    }
}
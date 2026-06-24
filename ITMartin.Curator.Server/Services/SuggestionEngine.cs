using System.Text.RegularExpressions;
using ITMartin.Curator.Server.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Helpers;

namespace ITMartin.Curator.Server.Services;

public sealed class SuggestionEngine(IConfiguration config)
{
    private static readonly Regex[] GenericPatterns =
    [
        new(@"^IMG_\d+",          RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^DSC_?\d+",         RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^DSCF\d+",          RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^MVI_\d+",          RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^VID_\d{8}_\d+",   RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^MOV_\d+",          RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^P\d{8}",           RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^DJI_\d+",          RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^GH0\d+",           RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^GOPR\d+",          RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^\d{8}_\d{6}$",     RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^photo_\d+",        RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^video_\d+",        RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^MVIMG_\d+",        RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^Screenshot_\d+",   RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^PXL_\d+",          RegexOptions.IgnoreCase | RegexOptions.Compiled),
    ];

    private static readonly Regex YearPattern  = new(@"^\d{4}$",    RegexOptions.Compiled);
    private static readonly Regex MonthPattern = new(@"^\d{2}-\w+$", RegexOptions.Compiled);

    public async Task<List<Suggestion>> AnalyzeAsync(IProgress<string>? progress = null, string? rootOverride = null)
    {
        var root = rootOverride ?? config["MediaSettings:LibraryRoot"] ?? "";
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            return [];

        progress?.Report("Scanning files…");

        var allFiles = await Task.Run(() =>
            Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                     .Where(MediaDisplayHelper.IsDisplayable)
                     .ToList());

        var suggestions = new List<Suggestion>();

        progress?.Report($"Analysing {allFiles.Count} files…");

        await Task.Run(() =>
        {
            CheckGenericNames(allFiles, root, suggestions);
            CheckDuplicates(allFiles, suggestions);
            CheckBursts(allFiles, suggestions);
            CheckDateClusters(allFiles, suggestions);
        });

        return suggestions;
    }

    // ── 1. Generic camera filenames ──────────────────────────────────────────

    private void CheckGenericNames(List<string> files, string root, List<Suggestion> out_)
    {
        var generic = files
            .Where(f => IsGenericName(Path.GetFileNameWithoutExtension(f)))
            .ToList();

        if (generic.Count == 0) return;

        var preview = BuildRenamePreview(generic, root);
        var folderCount = generic.Select(f => Path.GetDirectoryName(f)).Distinct().Count();

        out_.Add(new Suggestion
        {
            Type        = SuggestionType.RenameGeneric,
            Icon        = "📛",
            Title       = $"{generic.Count} files have generic camera names",
            Description = $"Across {folderCount} folder{(folderCount == 1 ? "" : "s")} — IMG_*, DSC_*, VID_*, and similar. Rename them automatically using date and sequence.",
            AffectedFiles = generic,
            RenamePreview = preview,
        });
    }

    private static bool IsGenericName(string nameWithoutExt) =>
        IsGenericNamePublic(nameWithoutExt);

    public static bool IsGenericNamePublic(string nameWithoutExt) =>
        GenericPatterns.Any(p => p.IsMatch(nameWithoutExt));

    public List<RenamePreviewItem> BuildRenamePreview(List<string> files, string root)
    {
        var result = new List<RenamePreviewItem>();

        foreach (var group in files.GroupBy(f => Path.GetDirectoryName(f) ?? ""))
        {
            var folderPath  = group.Key;
            var ordered     = group.OrderBy(f => new FileInfo(f).LastWriteTimeUtc).ToList();
            var usedNames   = Directory.EnumerateFiles(folderPath)
                                       .Select(f => Path.GetFileName(f).ToLowerInvariant())
                                       .ToHashSet();

            int seq = 1;
            foreach (var file in ordered)
            {
                string newName;
                do { newName = BuildAutoName(file, seq++, folderPath); }
                while (usedNames.Contains(newName.ToLowerInvariant()));

                usedNames.Add(newName.ToLowerInvariant());
                result.Add(new RenamePreviewItem(file, newName));
            }
        }

        return result;
    }

    private static string BuildAutoName(string filePath, int seq, string folderPath)
    {
        var ext   = Path.GetExtension(filePath);
        var parts = folderPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var year  = parts.LastOrDefault(p => YearPattern.IsMatch(p))  ?? "";
        var month = parts.LastOrDefault(p => MonthPattern.IsMatch(p)) ?? "";
        var mm    = month.Length >= 2 ? month[..2] : "";

        if (!string.IsNullOrEmpty(year) && !string.IsNullOrEmpty(mm))
            return $"{year}-{mm}_{seq:D3}{ext}";

        if (!string.IsNullOrEmpty(year))
            return $"{year}_{seq:D3}{ext}";

        var date = File.GetLastWriteTime(filePath);
        return $"{date:yyyy-MM-dd}_{seq:D3}{ext}";
    }

    // ── 2. Duplicate filenames ───────────────────────────────────────────────

    private static void CheckDuplicates(List<string> files, List<Suggestion> out_)
    {
        var groups = files
            .GroupBy(f => Path.GetFileName(f).ToLowerInvariant())
            .Where(g => g.Count() > 1)
            .Select(g => new DuplicateGroup { FileName = g.Key, Paths = g.ToList() })
            .ToList();

        if (groups.Count == 0) return;

        var total = groups.Sum(g => g.Paths.Count);

        out_.Add(new Suggestion
        {
            Type        = SuggestionType.DuplicateFiles,
            Icon        = "🔁",
            Title       = $"{groups.Count} duplicate filename{(groups.Count == 1 ? "" : "s")} found",
            Description = $"{total} files share names across different folders. Review and remove extras.",
            AffectedFiles  = groups.SelectMany(g => g.Paths).ToList(),
            DuplicateGroups = groups,
        });
    }

    // ── 3. Burst shots ───────────────────────────────────────────────────────

    private static void CheckBursts(List<string> files, List<Suggestion> out_)
    {
        var imageExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".jpg", ".jpeg", ".png", ".heic", ".avif", ".webp" };

        var images = files
            .Where(f => imageExts.Contains(Path.GetExtension(f)))
            .Select(f => (Path: f, Time: new FileInfo(f).LastWriteTimeUtc))
            .OrderBy(x => x.Time)
            .ToList();

        var bursts = new List<BurstGroup>();
        int i = 0;
        while (i < images.Count)
        {
            var group = new List<(string Path, DateTime Time)> { images[i] };
            while (i + 1 < images.Count &&
                   (images[i + 1].Time - images[i].Time).TotalSeconds <= 3)
            {
                i++;
                group.Add(images[i]);
            }

            if (group.Count >= 3)
            {
                bursts.Add(new BurstGroup
                {
                    Files     = group.Select(x => x.Path).ToList(),
                    Timestamp = group[0].Time,
                });
            }

            i++;
        }

        if (bursts.Count == 0) return;

        var totalFiles = bursts.Sum(b => b.Files.Count);

        out_.Add(new Suggestion
        {
            Type        = SuggestionType.BurstShots,
            Icon        = "⚡",
            Title       = $"{bursts.Count} burst shot group{(bursts.Count == 1 ? "" : "s")} detected",
            Description = $"{totalFiles} images taken within 3 seconds of each other. Keep the best from each group.",
            AffectedFiles = bursts.SelectMany(b => b.Files).ToList(),
            BurstGroups   = bursts,
        });
    }

    // ── 4. Large date clusters → folder suggestion ───────────────────────────

    private static void CheckDateClusters(List<string> files, List<Suggestion> out_)
    {
        var clusters = files
            .GroupBy(f =>
            {
                var parts = f.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var year  = parts.LastOrDefault(p => YearPattern.IsMatch(p))  ?? "";
                var month = parts.LastOrDefault(p => MonthPattern.IsMatch(p)) ?? "";
                return string.IsNullOrEmpty(year) ? null : $"{year}/{month}".TrimEnd('/');
            })
            .Where(g => g.Key != null && g.Count() >= 30)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .ToList();

        foreach (var cluster in clusters)
        {
            var label = cluster.Key!.Replace("/", " — ");
            out_.Add(new Suggestion
            {
                Type        = SuggestionType.GroupByDate,
                Icon        = "📅",
                Title       = $"Create a collection for {label}?",
                Description = $"{cluster.Count()} files from this period. Group them into a named collection.",
                AffectedFiles = cluster.ToList(),
            });
        }
    }
}

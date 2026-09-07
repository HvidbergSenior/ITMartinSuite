using System.Text.RegularExpressions;

namespace ITMartin.Media.Application.Pipelines.LibraryFinishing;

// Pure text editing, no I/O - deliberately NOT a real YAML parser. This file
// (docker-compose.yaml on the NAS) is ~1000+ lines serving ~30 real apps,
// full of comments and section banners a round-trip through a YAML library
// would reformat or reorder. Splicing exact new lines at exact verified
// points, and touching nothing else byte-for-byte, is the safer choice
// against a shared production file other people's running services depend
// on - see LibraryFinishingReportFormatter's own comment about this
// codebase's "the safer option, not the fancier one" convention.
//
// Modeled directly against the real, live gallery-web block fetched
// 2026-09-07 from 10.0.0.126:/volume1/docker/martinsuite/docker-compose.yaml
// (4 galleries already wired: mie, jespermette, vibeke, rico) - see
// GalleryComposeEditorTests for the exact fixture shape this assumes.
public static class GalleryComposeEditor
{
    public sealed record AddGalleryResult(string Yaml, bool AlreadyWired, int AssignedIndex);

    private static readonly Regex SlugLinePattern = new(@"Galleries__(\d+)__Slug=(.+)$", RegexOptions.Compiled);
    private static readonly Regex TopLevelServicePattern = new(@"^  [A-Za-z0-9_-]+:\s*$", RegexOptions.Compiled);
    private static readonly Regex VolumeLinePattern = new(@"^- .+:ro$", RegexOptions.Compiled);

    public static AddGalleryResult AddGallery(
        string composeYaml,
        string slug,
        string displayName,
        string containerLibraryPath,
        string hostLibraryPath,
        string password)
    {
        var lines = composeYaml.Replace("\r\n", "\n").Split('\n').ToList();

        var serviceStart = lines.FindIndex(l => l.TrimEnd() == "  gallery-web:");
        if (serviceStart < 0)
            throw new InvalidOperationException("gallery-web: service block not found in compose file");

        var serviceEnd = FindServiceBlockEnd(lines, serviceStart);

        var maxIndex = -1;
        for (var i = serviceStart; i < serviceEnd; i++)
        {
            var match = SlugLinePattern.Match(lines[i].Trim());
            if (!match.Success) continue;

            var index = int.Parse(match.Groups[1].Value);
            maxIndex = Math.Max(maxIndex, index);

            if (match.Groups[2].Value.Trim() == slug)
                return new AddGalleryResult(composeYaml, AlreadyWired: true, AssignedIndex: index);
        }

        var nextIndex = maxIndex + 1;

        var envInsertAfter = maxIndex >= 0
            ? FindLineContaining(lines, serviceStart, serviceEnd, $"Galleries__{maxIndex}__ShowSummary=")
            : FindLineContaining(lines, serviceStart, serviceEnd, "ASPNETCORE_URLS=");

        if (envInsertAfter < 0)
            throw new InvalidOperationException(
                maxIndex >= 0
                    ? $"Could not find the last line of the existing Galleries__{maxIndex}__ block"
                    : "Could not find environment: block for gallery-web (no ASPNETCORE_URLS= line)");

        var envIndent = LeadingWhitespace(lines[envInsertAfter]);
        var newEnvLines = new[]
        {
            $"{envIndent}- Galleries__{nextIndex}__Slug={slug}",
            $"{envIndent}- Galleries__{nextIndex}__Name={displayName}",
            $"{envIndent}- Galleries__{nextIndex}__Path={containerLibraryPath}",
            $"{envIndent}- Galleries__{nextIndex}__Password={password}",
            $"{envIndent}- Galleries__{nextIndex}__ShowSummary=true",
        };
        lines.InsertRange(envInsertAfter + 1, newEnvLines);
        serviceEnd += newEnvLines.Length;

        var lastVolumeLine = -1;
        for (var i = serviceStart; i < serviceEnd; i++)
        {
            if (VolumeLinePattern.IsMatch(lines[i].Trim()))
                lastVolumeLine = i;
        }
        if (lastVolumeLine < 0)
            throw new InvalidOperationException("Could not find volumes: block for gallery-web");

        var volIndent = LeadingWhitespace(lines[lastVolumeLine]);
        lines.Insert(lastVolumeLine + 1, $"{volIndent}- {hostLibraryPath}:{containerLibraryPath}:ro");

        return new AddGalleryResult(string.Join('\n', lines), AlreadyWired: false, AssignedIndex: nextIndex);
    }

    // The block ends at the next top-level (exactly 2-space indent) service
    // declaration, or end of file - a section-comment banner between
    // services (see the real file's "# === BUDGET ===" style dividers)
    // isn't itself a service line, so it's correctly included as part of
    // gallery-web's trailing whitespace rather than mistaken for a boundary.
    private static int FindServiceBlockEnd(List<string> lines, int serviceStart)
    {
        for (var i = serviceStart + 1; i < lines.Count; i++)
        {
            if (TopLevelServicePattern.IsMatch(lines[i]))
                return i;
        }
        return lines.Count;
    }

    private static int FindLineContaining(List<string> lines, int start, int end, string needle)
    {
        for (var i = start; i < end; i++)
        {
            if (lines[i].Contains(needle, StringComparison.Ordinal))
                return i;
        }
        return -1;
    }

    private static string LeadingWhitespace(string line) =>
        line[..(line.Length - line.TrimStart().Length)];
}

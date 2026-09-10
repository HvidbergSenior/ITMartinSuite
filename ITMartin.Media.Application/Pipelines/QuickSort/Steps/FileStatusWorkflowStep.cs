using ITMartin.Media.Application.Pipelines.QuickSort.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Helpers;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.QuickSort.Steps;

// Writes each exported file's step-flag verdicts into the library's
// persisted filestatus.json registry (see IFileStatusRegistryService) - the
// "isDone" record every later run (this pipeline's own re-runs, the
// standalone LibraryPolishService.RunAllStepsAsync pass, etc.) checks before
// doing any work, so a file that reaches IsDone is never re-processed by
// anything, on any future run, unless explicitly forced.
public sealed class FileStatusWorkflowStep : QuickSortWorkflowStepBase
{
    // Categories that are actually date-organized (Year/Month folders) - only
    // these care whether a file's date is reliable. Musik/Dokumenter/
    // Skærmbilleder/Chat/Memes/LivePhotos are flat or artist/album-organized.
    private static readonly HashSet<string> DateOrganizedCategories =
        new(StringComparer.OrdinalIgnoreCase) { "Billeder", "Videoer" };

    private static readonly HashSet<MediaSubCategory> UnknownSubCategories =
        new()
        {
            MediaSubCategory.UnknownImage, MediaSubCategory.UnknownVideo,
            MediaSubCategory.UnknownAudio, MediaSubCategory.UnknownDocument,
            MediaSubCategory.UnknownOther,
        };

    private readonly IFileStatusRegistryService _registry;
    private readonly ILibraryPathProvider _libraryPathProvider;
    private readonly ILogger<FileStatusWorkflowStep> _logger;

    public FileStatusWorkflowStep(
        IFileStatusRegistryService registry,
        ILibraryPathProvider libraryPathProvider,
        ILogger<FileStatusWorkflowStep> logger)
    {
        _registry = registry;
        _libraryPathProvider = libraryPathProvider;
        _logger = logger;
    }

    public override string Name => "FileStatus";

    public override async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
    {
        var state = context.State as QuickSortWorkflowState
            ?? throw new InvalidOperationException("Invalid workflow state");

        var exportRoot = !string.IsNullOrWhiteSpace(state.OutputPath)
            ? state.OutputPath
            : _libraryPathProvider.LibraryRoot;
        if (string.IsNullOrWhiteSpace(exportRoot) || !Directory.Exists(exportRoot))
            return;

        var registryDict = await _registry.LoadAsync(exportRoot, cancellationToken);

        // Duplicates/DeleteCandidates still skip this record entirely - their
        // hash by definition matches the kept canonical file's, and this
        // dictionary is keyed by ContentHash, so recording them here would
        // overwrite (or race with) the real file's status. LargeFilm/
        // SmallArtist/Unplayable don't have that problem (they're not
        // hash-duplicates of anything kept), so they get a real record with
        // NotFiltered=false instead of vanishing from filestatus.json with no
        // trace.
        var eligible = state.MediaFiles
            .Where(f => f.ExportSubFolder is not ("Duplicates" or "DeleteCandidates"))
            .Where(f => !string.IsNullOrWhiteSpace(f.Hash))
            .ToList();

        var newlyDone = 0;
        var duplicatesOfExisting = 0;

        foreach (var file in eligible)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var category = CategoryHelper.GetCategory(file);
            var finalPath = file.ExportedPath ?? file.FullPath;
            var relativePath = Path.GetRelativePath(exportRoot, finalPath);

            FileInfo? info = null;
            try { info = new FileInfo(finalPath); } catch (IOException) { }

            var isCrossRunDuplicate = registryDict.ContainsKey(file.Hash!) &&
                                       !string.Equals(registryDict[file.Hash!].RelativePath, relativePath, StringComparison.OrdinalIgnoreCase);
            if (isCrossRunDuplicate) duplicatesOfExisting++;

            var isImage = file.Type == MediaType.Image;
            var isDateOrganized = DateOrganizedCategories.Contains(category);

            // IsNormalized only ever gets set true by ImageNormalizationWorkflowStep/
            // IConcurrentVideoDispatcher, both of which skip everything but
            // Image/Video by design (audio/documents are just copied, never
            // "normalized" to begin with) - so it must only be applicable for
            // those two types, or every audio/document file would sit at
            // IsDone=false forever and FaceIndex's convergence loop would keep
            // re-touching them on every single run.
            var applicable = new List<string> { StepFlags.CategoryIsSet, StepFlags.SubCategoryIsSet, StepFlags.NotDuplicate, StepFlags.FileIsReadable, StepFlags.NotFiltered };
            if (isImage || file.Type == MediaType.Video) applicable.Add(StepFlags.IsNormalized);
            if (isDateOrganized) applicable.Add(StepFlags.DateIsSet);
            if (isImage)
            {
                applicable.Add(StepFlags.RotationIsCorrect);
                applicable.Add(StepFlags.QualityChecked);
            }

            var flags = new Dictionary<string, FlagState>
            {
                [StepFlags.CategoryIsSet] = new() { Value = file.MainCategory != MediaMainCategory.Other },
                [StepFlags.SubCategoryIsSet] = new() { Value = !UnknownSubCategories.Contains(file.SubCategory) },
                [StepFlags.NotDuplicate] = new() { Value = !isCrossRunDuplicate, Suggestion = isCrossRunDuplicate ? $"Exact-hash duplicate of {registryDict[file.Hash!].RelativePath}" : null },
                [StepFlags.IsNormalized] = new() { Value = file.IsNormalized, Suggestion = file.IsNormalized ? null : "Not yet converted to this type's canonical format" },
                [StepFlags.FileIsReadable] = new() { Value = !state.FailedFiles.Any(f => f.FilePath == file.FullPath) },
            };

            var filterReason = file.ExportSubFolder switch
            {
                "LargeFilm" => $"Routed to Review/LargeFilm - {file.Duration?.TotalMinutes:F0} min video exceeds the 20 min personal-video threshold",
                "SmallArtist" => $"Routed to Review/SmallArtist - too few tracks found for artist '{file.Artist}' (6 or fewer across the whole library)",
                "Unplayable" => "Routed to Review/Unplayable - could not be read or decoded, likely corrupt or truncated",
                _ => null,
            };
            flags[StepFlags.NotFiltered] = new() { Value = filterReason is null, Suggestion = filterReason };
            if (isDateOrganized)
                flags[StepFlags.DateIsSet] = new() { Value = file.IsDateReliable, Suggestion = file.IsDateReliable ? null : "No reliable date source (EXIF/GPS/face-match) found" };
            if (isImage)
            {
                flags[StepFlags.RotationIsCorrect] = file.OrientationKnownFromExif && !file.OrientationSourceIsUnreliable
                    ? new() { Value = true }
                    : new()
                    {
                        Value = false,
                        Suggestion = file.OrientationSourceIsUnreliable
                            ? "Camera writes an unreliable EXIF orientation tag - run the rotation-fix pass"
                            : "No EXIF orientation tag found - run the rotation-fix pass",
                    };

                // ImageQualityWorkflowStep's free local check always runs, so
                // both are normally set by this point; AiClassificationWorkflowStep
                // (paid, opt-in) overwrites them with its own more reliable
                // verdict when it also ran. Null here only means the file
                // couldn't be decoded at all - treated as unresolved, not
                // as a confirmed-good pass.
                var qualityOk = file.IsBlurry == false && file.IsSolidColor == false;
                flags[StepFlags.QualityChecked] = file.IsBlurry.HasValue && file.IsSolidColor.HasValue
                    ? new() { Value = qualityOk, Suggestion = qualityOk ? null : file.IsBlurry == true ? "Image appears blurry" : "Image appears to be a solid color/blank" }
                    : new() { Value = false, Suggestion = "Could not be analyzed for quality" };
            }

            registryDict[file.Hash!] = new FileStatusRecord
            {
                ContentHash = file.Hash!,
                RelativePath = relativePath,
                Category = category,
                ApplicableFlags = applicable,
                Flags = flags,
                SizeBytes = info?.Length ?? file.SizeBytes,
                LastWriteUtc = info?.LastWriteTimeUtc ?? DateTimeOffset.UtcNow,
                LastUpdatedUtc = DateTimeOffset.UtcNow,
            };

            if (registryDict[file.Hash!].IsDone) newlyDone++;
        }

        await _registry.SaveAsync(exportRoot, registryDict, cancellationToken);

        _logger.LogInformation(
            "File status recorded for {Root}: {Total} files tracked, {Done} fully done, {Duplicates} matched an existing hash from a prior run",
            exportRoot, eligible.Count, newlyDone, duplicatesOfExisting);

        await WriteManualRotationListAsync(exportRoot, eligible, cancellationToken);
    }

    // Cameras stopped being the problem around here. Devices from roughly
    // 2011 on carry an orientation sensor and write a real tag, which
    // BakeExifOrientation then applies automatically - so a missing tag on a
    // modern photo means something harmless (a screenshot, a download, an app
    // that stripped EXIF), not a sideways picture.
    private const int LastYearOrientationWasUnreliable = 2010;

    // Which photos genuinely need a human to look at them.
    //
    // Measured on the ToshibaTest library before this filter existed: keying
    // on "no EXIF orientation tag" alone flagged 10,511 of 16,990 photos -
    // 62%, which is not a shortlist, it is the whole library. The noise was
    // all modern: 824 from 2025 and 2,232 loose-root files, none of them from
    // an affected camera.
    //
    // The signal that actually predicted a sideways photo was the CAMERA.
    // Every one of the ~600 rotations found by hand traced back to two
    // Olympus bodies from 2007-2010. So an unreliable camera is enough on its
    // own, and a missing tag only counts alongside a pre-2011 date.
    // Public so the filter can be tested directly - it is the part that got
    // this wrong once already, at 62% of the library.
    public static bool NeedsManualRotationReview(MediaFile file)
    {
        // The camera is known to lie about orientation - enough on its own,
        // whatever the date says.
        if (file.OrientationSourceIsUnreliable) return true;

        // A tag the pipeline could read has already been applied.
        if (file.OrientationKnownFromExif) return false;

        // No tag, and no date to judge the era by - leave it alone rather
        // than sweep in every undated screenshot in the library.
        if (file.CreatedAt is null) return false;

        return file.CreatedAt.Value.Year <= LastYearOrientationWasUnreliable;
    }

    // Photos whose orientation the pipeline cannot determine, written out for
    // a human to fix rather than guessed at.
    //
    // The rule (user, 2026-09-10): "Should know which cameras and which year
    // and if no exif -> Dont do anything -> Handle by them self". Inferring
    // rotation from image content means face detection, which measured ~24
    // seconds per photo on real hardware and is why the old rotation pass was
    // removed from the pipeline entirely. So this step guesses nothing - it
    // lists the files that cannot be resolved from metadata, grouped so the
    // affected cameras and years are obvious, and leaves the decision to the
    // person who can actually see the picture.
    //
    // Confirmed shape of the problem on ToshibaTest: 451 hand-rotated photos,
    // every one from 2007-2010 on two Olympus compacts with no orientation
    // sensor, and nothing at all from 2011 onward.
    private async Task WriteManualRotationListAsync(
        string exportRoot,
        List<MediaFile> eligible,
        CancellationToken cancellationToken)
    {
        var needsReview = eligible
            .Where(f => f.Type == MediaType.Image)
            .Where(NeedsManualRotationReview)
            .Where(f => !string.IsNullOrWhiteSpace(f.ExportedPath))
            .OrderBy(f => f.ExportedPath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (needsReview.Count == 0) return;

        var lines = new List<string> { "RelativePath,Year,Camera,Reason" };
        foreach (var f in needsReview)
        {
            var relative = Path.GetRelativePath(exportRoot, f.ExportedPath!);
            var year = f.CreatedAt?.Year.ToString() ?? "";
            var camera = (f.CameraModel ?? "(ukendt)").Replace("\"", "\"\"");
            var reason = f.OrientationSourceIsUnreliable
                ? "Camera writes an unreliable orientation tag"
                : "No EXIF orientation tag";
            lines.Add($"\"{relative.Replace("\"", "\"\"")}\",{year},\"{camera}\",{reason}");
        }

        var path = Path.Combine(exportRoot, "Roter-manuelt.csv");
        try
        {
            await File.WriteAllLinesAsync(path, lines, cancellationToken);

            await BuildManualRotationFolderAsync(exportRoot, needsReview, cancellationToken);

            var byYear = needsReview
                .GroupBy(f => f.CreatedAt?.Year.ToString() ?? "(ukendt)")
                .OrderBy(g => g.Key)
                .Select(g => $"{g.Key}:{g.Count()}");

            var byCamera = needsReview
                .GroupBy(f => string.IsNullOrWhiteSpace(f.CameraModel) ? "(ukendt)" : f.CameraModel!)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => $"{g.Key}:{g.Count()}");

            _logger.LogInformation(
                "{Count} image(s) have no trustworthy orientation and were NOT rotated - listed in Roter-manuelt.csv for manual review. By year: {ByYear}. Top cameras: {ByCamera}",
                needsReview.Count,
                string.Join(", ", byYear),
                string.Join(", ", byCamera));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not write the manual-rotation list to {Path}", path);
        }
    }

    // Everything the pipeline could not resolve, gathered into ONE flat
    // folder so a person can page through it in a photo viewer and rotate
    // what needs rotating, instead of hunting the same photos across twenty
    // year folders.
    //
    // This is the workflow that was done by hand on ToshibaTest 2026-09-10 -
    // ~600 photos, copied out flat, paged through, rotated, copied back - now
    // produced by the sort itself. Copies, not moves: these are real photos
    // that belong in their year folders, and the folder is a review surface,
    // not a new home for them.
    //
    // Names are prefixed with a zero-padded number so they keep library order
    // and cannot collide - basenames repeat across year folders constantly.
    // RoterManuelt.csv beside them maps each copy back to the real file, which
    // is what an apply-back pass needs.
    //
    // NOTE for whoever writes that apply-back: Windows' Photos app "rotate"
    // usually only flips the EXIF Orientation tag and leaves the pixels alone.
    // The photo then looks right in Explorer while every raw-pixel reader -
    // the gallery thumbnailer included - still sees it sideways. Bake the tag
    // into the pixels before copying anything back, or the fix is invisible
    // where it matters.
    private async Task BuildManualRotationFolderAsync(
        string exportRoot,
        List<MediaFile> needsReview,
        CancellationToken cancellationToken)
    {
        var folder = Path.Combine(exportRoot, "SmartFolders", "RoterManuelt");

        try
        {
            Directory.CreateDirectory(folder);

            var map = new List<string> { "CopyName,Original" };
            var i = 0;
            var copied = 0;

            foreach (var f in needsReview)
            {
                cancellationToken.ThrowIfCancellationRequested();
                i++;

                var source = f.ExportedPath!;
                if (!File.Exists(source)) continue;

                var copyName = $"{i:D5}__{Path.GetFileName(source)}";
                var destination = Path.Combine(folder, copyName);

                try
                {
                    File.Copy(source, destination, overwrite: true);
                    map.Add($"\"{copyName.Replace("\"", "\"\"")}\",\"{source.Replace("\"", "\"\"")}\"");
                    copied++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not stage {Path} for manual rotation", source);
                }
            }

            await File.WriteAllLinesAsync(
                Path.Combine(folder, "RoterManuelt.csv"),
                map,
                cancellationToken);

            _logger.LogInformation(
                "Staged {Copied} image(s) into {Folder} for manual rotation - page through them in a photo viewer, rotate what is wrong, then run the apply-back pass",
                copied,
                folder);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not build the manual-rotation folder at {Folder}", folder);
        }
    }
}

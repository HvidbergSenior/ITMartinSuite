using ITMartin.Media.Application.Pipelines.Package1.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Helpers;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package1.Steps;

// Writes each exported file's step-flag verdicts into the library's
// persisted filestatus.json registry (see IFileStatusRegistryService) - the
// "isDone" record every later run (this pipeline's own re-runs, the
// standalone LibraryPolishService.RunAllStepsAsync pass, etc.) checks before
// doing any work, so a file that reaches IsDone is never re-processed by
// anything, on any future run, unless explicitly forced.
public sealed class FileStatusWorkflowStep : Package1WorkflowStepBase
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
        var state = context.State as Package1WorkflowState
            ?? throw new InvalidOperationException("Invalid workflow state");

        var exportRoot = !string.IsNullOrWhiteSpace(state.OutputPath)
            ? state.OutputPath
            : _libraryPathProvider.LibraryRoot;
        if (string.IsNullOrWhiteSpace(exportRoot) || !Directory.Exists(exportRoot))
            return;

        var registryDict = await _registry.LoadAsync(exportRoot, cancellationToken);

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
            // VideoNormalizationWorkflowStep, both of which skip everything but
            // Image/Video by design (audio/documents are just copied, never
            // "normalized" to begin with) - so it must only be applicable for
            // those two types, or every audio/document file would sit at
            // IsDone=false forever and Package3's convergence loop would keep
            // re-touching them on every single run.
            var applicable = new List<string> { StepFlags.CategoryIsSet, StepFlags.SubCategoryIsSet, StepFlags.NotDuplicate, StepFlags.FileIsReadable };
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
    }
}

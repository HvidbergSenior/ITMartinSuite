using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Runtime.BackgroundJobs;

// The steps that run AFTER QuickSort's own 18, to finish a library in one go.
//
// TWO REASONS THIS TYPE EXISTS.
//
// 1. These used to live inline in StartQuickSortHandler, so they only ran
//    when a sort arrived through the job queue. A workflow picked up by
//    WorkflowRecoveryHostedService goes straight to the executor and never
//    touches that handler - so a recovered run completed all 18 sort steps,
//    reported success, and silently skipped everything below. That is exactly
//    what happened to ToshibaTest on 2026-09-09: the run was recovered after
//    a restart, finished cleanly, and left MediaFaces completely empty. Face
//    indexing had not failed, it was never invoked, and nothing in the logs
//    said so because nothing had gone wrong. Both paths now run the same set.
//
// 2. Only what is NECESSARY, CHEAP and SHORT runs automatically (user, 2026-09-10:
//    "I want all necessary easy and short steps to run in one go"). Everything
//    per-image-expensive or paid is deliberately NOT here - see the list at
//    the bottom. This pipeline has been bitten twice by a slow per-image pass
//    sitting in an automatic path: a rotation scan that managed ~600 of 38,367
//    images in four hours and held the whole delivery behind it, and an
//    ImageQuality step that spent 6m33s per run producing a verdict nothing
//    read.
public sealed class QuickSortAddonSteps
{
    // Where a person's reference photos go: one folder per person, named
    // exactly what their generated folder should be called.
    public const string ReferencePhotosFolderName = ".ReferencePhotos";

    private static readonly string[] ReferencePhotoExtensions =
        [".jpg", ".jpeg", ".png", ".heic"];

    private readonly IFaceIndexService _faceIndex;
    private readonly ISmartFoldersService _smartFolders;
    private readonly ILibraryPolishService _polish;
    private readonly IStaticGalleryExportService _galleryExport;
    private readonly ILogger<QuickSortAddonSteps> _logger;

    public QuickSortAddonSteps(
        IFaceIndexService faceIndex,
        ISmartFoldersService smartFolders,
        ILibraryPolishService polish,
        IStaticGalleryExportService galleryExport,
        ILogger<QuickSortAddonSteps> logger)
    {
        _faceIndex = faceIndex;
        _smartFolders = smartFolders;
        _polish = polish;
        _galleryExport = galleryExport;
        _logger = logger;
    }

    public async Task RunAsync(string outputPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(outputPath) || !Directory.Exists(outputPath))
        {
            _logger.LogWarning(
                "Skipping post-sort steps - no usable output path ({OutputPath})",
                outputPath);
            return;
        }

        _logger.LogInformation("Post-sort steps starting for {OutputPath}", outputPath);

        // Metadata-only and deterministic: acts only where a file already
        // carries a real, non-1 EXIF Orientation tag. Guesses nothing - the
        // photos it cannot resolve are staged for a human by
        // FileStatusWorkflowStep instead.
        await RunStepAsync("BakeExifOrientation", outputPath,
            () => _polish.BakeExifOrientationAsync(outputPath, cancellationToken));

        // Empty folders, OS junk, hidden manifest. Cheap directory work.
        await RunStepAsync("LibraryPolish", outputPath,
            () => _polish.PolishAsync(outputPath, cancellationToken));

        // Both read metadata already extracted during the sort - GPS for
        // trips, dates for Jul/Nytår - so they are fast regardless of library
        // size. On ToshibaTest: 4 trips and 2 traditions in seconds.
        await RunStepAsync("GenerateTripFolders", outputPath,
            () => _smartFolders.GenerateTripFoldersAsync(outputPath, cancellationToken));

        await RunStepAsync("GenerateTraditions", outputPath,
            () => _smartFolders.GenerateTraditionsAsync(outputPath, cancellationToken));

        await RunStepAsync("SyncGalleryCollections", outputPath,
            () => _smartFolders.SyncGalleryCollectionsAsync(outputPath, cancellationToken));

        // Before the gallery export, so any person folders it creates are
        // included in the browsable output rather than missing until the next
        // run. Costs nothing unless reference photos exist - see the method.
        await RunStepAsync("PersonFolders", outputPath,
            () => GeneratePersonFoldersAsync(outputPath, cancellationToken));

        // Last, and the one genuinely non-trivial step kept here: without it
        // the delivered drive has no index.html and nothing to browse, so it
        // is necessary rather than optional.
        await RunStepAsync("StaticGalleryExport", outputPath,
            () => _galleryExport.ExportAsync(outputPath, cancellationToken));

        _logger.LogInformation("Post-sort steps complete for {OutputPath}", outputPath);
    }

    // DELIBERATELY NOT RUN AUTOMATICALLY - each is either per-image expensive
    // or costs money. Run them explicitly against a delivered library when
    // they are actually wanted:
    //
    //   EstimateUndatedDates (IFaceIndexService.EstimateUndatedDatesAsync)
    //     Face-matching plus a GPS pass. Depends on the face index, so it
    //     inherits that cost, and unlike person folders nothing signals that
    //     a particular library wants it.
    //
    //   GenerateUnknownPersonFolders (ISmartFoldersService)
    //     Reads the face index. Cheap on its own, but returns nothing at all
    //     until IndexFaces has run - on ToshibaTest it dutifully produced
    //     "0 unknown-person folders" because MediaFaces was empty. Worth
    //     revisiting once a library has an index from the person pass.
    //
    // IndexFaces itself is NOT in this list: it runs, but only when
    // .ReferencePhotos says someone wants person folders. See
    // GeneratePersonFoldersAsync for why that gate is the whole design.
    //
    //   ClassifyUnhandledFilesAsync (IFaceIndexService)
    //     Makes real Claude API calls. CLAUDE.md keeps paid passes
    //     manual/opt-in, and this one sat in the automatic path despite that.
    //
    //   GenerateSimilarSceneFolders - removed outright 2026-09-09
    //     ("IT IS NOT NEEDED - REMOVE"): copied perceptual-hash clusters into
    //     SmartFolders/Lignende, 605 MB of duplicated copies with poor
    //     grouping, and nothing in the delivered gallery ever linked it.

    // Person folders, driven entirely by what is in .ReferencePhotos.
    //
    // The folder IS the opt-in, which is what makes this both automatic and
    // cheap. Face indexing extracts an embedding from every image in the
    // library - far too expensive to run on every sort just in case someone
    // might want person folders later. But it is also the prerequisite for
    // them, so demanding a separate manual pass would leave the feature
    // permanently one step away.
    //
    // Dropping a folder of photos into .ReferencePhotos is an unambiguous
    // "yes, I want person folders", so the cost is only ever paid when it has
    // been asked for. No reference photos, no indexing, no cost. Matches the
    // user's rule: "no endpoint calls, I want automatic progress".
    //
    // Everything here is incremental: IndexFacesAsync skips already-indexed
    // files, and people already registered are not added twice, so a re-run
    // against an unchanged library costs almost nothing.
    private async Task GeneratePersonFoldersAsync(string libraryPath, CancellationToken cancellationToken)
    {
        var referenceRoot = Path.Combine(libraryPath, ReferencePhotosFolderName);
        if (!Directory.Exists(referenceRoot)) return;

        var people = Directory.EnumerateDirectories(referenceRoot)
            .Select(dir => new
            {
                Name = Path.GetFileName(dir),
                Photos = Directory.EnumerateFiles(dir)
                    .Where(f => ReferencePhotoExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                    .ToList(),
            })
            .Where(p => p.Photos.Count > 0 && !string.IsNullOrWhiteSpace(p.Name))
            .ToList();

        if (people.Count == 0)
        {
            _logger.LogInformation(
                "No reference photos in {Root} - skipping face indexing and person folders entirely",
                referenceRoot);
            return;
        }

        _logger.LogInformation(
            "{Count} person(s) have reference photos - running face indexing (this is the expensive pass, and only runs because reference photos exist)",
            people.Count);

        // The prerequisite. Local FaceONNX, no API cost, and resumable - an
        // interrupted run just picks up where it stopped.
        await _faceIndex.IndexFacesAsync(libraryPath, cancellationToken: cancellationToken);

        var existing = await _faceIndex.GetPeopleAsync();

        foreach (var person in people)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var known = existing.FirstOrDefault(p =>
                    string.Equals(p.Name, person.Name, StringComparison.OrdinalIgnoreCase));

                var personId = known?.Id;

                if (personId is null)
                {
                    var inputs = new List<ReferencePhotoInput>();
                    foreach (var photo in person.Photos)
                    {
                        inputs.Add(new ReferencePhotoInput(
                            Path.GetFileName(photo),
                            await File.ReadAllBytesAsync(photo, cancellationToken)));
                    }

                    personId = await _faceIndex.AddPersonAsync(person.Name, inputs, libraryPath);

                    _logger.LogInformation(
                        "Registered {Name} from {Count} reference photo(s)",
                        person.Name, inputs.Count);
                }

                var folder = await _smartFolders.GeneratePersonFolderAsync(
                    libraryPath, personId.Value, cancellationToken: cancellationToken);

                if (folder is null)
                {
                    _logger.LogWarning(
                        "No photos matched {Name} - try more or clearer reference photos",
                        person.Name);
                }
                else
                {
                    _logger.LogInformation(
                        "{Name}: {Count} photo(s) at {Folder}",
                        person.Name, folder.FileCount, folder.FolderPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not build a person folder for {Name}", person.Name);
            }
        }
    }

    // One failing step must not lose the rest - these run unattended after a
    // sort that may have taken hours.
    private async Task RunStepAsync(string stepName, string outputPath, Func<Task> step)
    {
        try
        {
            await step();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Post-sort step {Step} failed for {OutputPath}", stepName, outputPath);
        }
    }
}

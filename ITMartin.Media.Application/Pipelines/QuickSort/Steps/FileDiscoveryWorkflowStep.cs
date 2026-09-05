using ITMartin.Media.Application.Interfaces;
using ITMartin.Media.Application.Pipelines.QuickSort.Models;
using ITMartin.Media.Application.Pipelines.QuickSort.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Persistence;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.QuickSort.Steps;

public sealed class FileDiscoveryWorkflowStep
    : QuickSortWorkflowStepBase
{
    // Confirmed 2026-09-03 on Rico/AC's whole-drive backup archive
    // (~10,600 files, ~28% of everything discovered): browser cache, old
    // program/OS debris, and application config/data files swept in from a
    // raw source folder that's a full disk image rather than a curated photo
    // folder. Unlike the general "Unknown" fallback below (kept on purpose,
    // for real content FaceIndex should still review), these extensions are
    // never real family content under any circumstance - skipped outright
    // instead of carried through to Ikke_identificeret, so a future run on a
    // similar whole-drive backup doesn't need the same manual cleanup pass
    // again.
    private static readonly HashSet<string> NeverMediaExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Browser cache/storage
            ".file", ".localstorage", ".localstorage-journal", ".cache",
            ".binarycookies", ".sqlite-shm", ".sqlite-wal", ".sqlite-journal",
            ".crx", ".xul", ".xpi", ".xpt", ".bdic",
            // Web page/script debris
            ".js", ".css", ".html", ".htm", ".xhtml", ".rss", ".xsl", ".rdf",
            // App config/data/db
            ".ini", ".dat", ".wbcat", ".db", ".sqlite", ".idx", ".plist",
            ".sqm", ".blf", ".properties", ".manifest", ".cfg", ".config",
            ".settings", ".sav", ".chk", ".cat", ".bin", ".old", ".bak",
            ".tmp", ".part", ".lck", ".pat", ".mgc", ".mf", ".inf", ".icc",
            ".fingerprint", ".etl", ".dtd", ".crl", ".acl", ".certs", ".sig",
            ".rsa", ".pak", ".xml", ".dmp", ".crash", ".log", ".log1",
            ".regtrans-ms", ".oeaccount", ".%%%oestandardproperty",
            ".%%%oecustomproperty", ".extra",
            // Shortcuts/icons/programs
            ".lnk", ".url", ".ico", ".dll", ".exe", ".msi", ".ocx",
            // Old iTunes/Windows Media Player library debris
            ".itc2", ".itl", ".itdb", ".wpl", ".wmdb", ".bnk",
            // Misc single/rare extensions confirmed junk on that archive
            ".info", ".lst", ".gz", ".jsm", ".jsw", ".jrs", ".swz", ".stl",
            ".psp", ".heu", ".pob", ".upp", ".qtch", ".pset", ".pip",
            ".msmessagestore", ".library-ms", ".feed-ms", ".xpt", ".vch",
            ".ps", ".aum", ".zm", ".theme", ".syncdb", ".sh", ".sf",
            ".search-ms", ".otf", ".hxw", ".devicemetadata-ms", ".cst",
            ".csf", ".cch", ".acrodata", ".dbx", ".sst", ".wmf", ".emf",
            ".edb", ".dan", ".directory",
            // Old camcorder-generated video preview sidecars (e.g.
            // MVI_1234.AVI + MVI_1234.THM) - confirmed 2026-09-03 redundant
            // with GalleryThumbnailWorkflowStep, which already generates a
            // real thumbnail per video from the actual (converted) content,
            // not the camera's tiny low-res original.
            ".thm",
        };

    private readonly ILogger<FileDiscoveryWorkflowStep>
        _logger;

    private readonly IFileScanner
        _fileScanner;

    private readonly IMediaTypeResolver
        _mediaTypeResolver;

    private readonly IMediaDateService
        _mediaDateService;

    private readonly IWorkflowInstanceStore
        _workflowInstanceStore;

    public FileDiscoveryWorkflowStep(
        IFileScanner fileScanner,
        IMediaTypeResolver mediaTypeResolver,
        IMediaDateService mediaDateService,
        IWorkflowInstanceStore workflowInstanceStore,
        ILogger<FileDiscoveryWorkflowStep> logger)
    {
        _fileScanner =
            fileScanner;

        _mediaTypeResolver =
            mediaTypeResolver;

        _mediaDateService =
            mediaDateService;

        _workflowInstanceStore =
            workflowInstanceStore;

        _logger =
            logger;
    }

    public override string Name =>
        "FileDiscovery";

    public override async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
    {
        var state =
            context.State as QuickSortWorkflowState
            ?? throw new InvalidOperationException(
                "Invalid workflow state");

        if (state.MediaFiles.Count > 0)
        {
            return;
        }

        await ExecuteOperationAsync(
            "ScanFiles",
            state.RootPath,
            async () =>
            {
                var files =
                    (await _fileScanner.ScanAsync(
                        state.RootPath,
                        cancellationToken)).ToList();

                var total = files.Count;
                var current = 0;
                var result = new List<MediaFile>(total);
                var categoryCounts = new Dictionary<string, int>();

                foreach (var path in files)
                {
                    current++;

                    // One malformed file (bad date-in-filename, unreadable, etc.) must
                    // not abort discovery for the other thousands - skip it and log,
                    // rather than losing the whole scan to a single bad file.
                    try
                    {
                        if (NeverMediaExtensions.Contains(Path.GetExtension(path)))
                        {
                            _logger.LogInformation("Skipping never-media file: {Path}", path);
                            continue;
                        }

                        var mediaType = _mediaTypeResolver.Resolve(path);

                        // Not a recognized media/document type (DB table files,
                        // app config/cache junk swept in from a raw source
                        // folder, etc.) - still gets carried through to export
                        // as "Unhandled" (MediaMainCategory.Other) rather than
                        // silently dropped, so nothing from the source tree
                        // goes unaccounted for and FaceIndex has a real place
                        // to review/reclassify these later.
                        var typeName = mediaType.ToString();
                        categoryCounts[typeName] =
                            categoryCounts.GetValueOrDefault(typeName) + 1;

                        LogStepProgress(
                            _logger,
                            Name,
                            current,
                            total,
                            Path.GetFileName(path));

                        if (current % 10 == 0 || current == total)
                        {
                            await _workflowInstanceStore.SetProgressAsync(
                                context.WorkflowId,
                                current,
                                total,
                                item: Path.GetFileName(path),
                                counts: categoryCounts,
                                cancellationToken: cancellationToken);
                        }

                        var dateResult =
                            _mediaDateService.GetBestDate(
                                new MediaDateRequest(
                                    path,
                                    state.OverrideYear));

                        result.Add(new MediaFile(
                            path,
                            dateResult.Date,
                            mediaType,
                            new FileInfo(path).Length,
                            dateResult.IsReliable));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Skipping file during discovery: {Path}", path);
                    }
                }

                state.MediaFiles = result;
            },
            _logger);
    }
}

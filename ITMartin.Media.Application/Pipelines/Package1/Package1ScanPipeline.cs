using ITMartin.Media.Application.Interfaces;
using ITMartin.Media.Application.Models;
using ITMartin.Media.Application.Processors;

namespace ITMartin.Media.Application.Pipelines;

public class Package1ScanPipeline
{
    private readonly FileEnumerationProcessor
        _fileEnumerationProcessor;

    private readonly ParallelScanProcessor
        _parallelScanProcessor;

    private readonly IDuplicateService
        _duplicateService;

    private readonly Package1CleanupPipeline
        _cleanupPipeline;

    public Package1ScanPipeline(
        FileEnumerationProcessor
            fileEnumerationProcessor,

        ParallelScanProcessor
            parallelScanProcessor,

        IDuplicateService
            duplicateService,

        Package1CleanupPipeline
            cleanupPipeline)
    {
        _fileEnumerationProcessor =
            fileEnumerationProcessor;

        _parallelScanProcessor =
            parallelScanProcessor;

        _duplicateService =
            duplicateService;

        _cleanupPipeline =
            cleanupPipeline;
    }

    public async Task<Package1ScanResult>
        RunAsync(
            string folderPath,
            Action<int, int, string>?
                progress = null)
    {
        _duplicateService.Reset();

        var paths =
            _fileEnumerationProcessor
                .Enumerate(folderPath);

        var files =
            await _parallelScanProcessor
                .ProcessAsync(
                    paths,
                    progress);

        _duplicateService.AllFiles =
            files;

        _duplicateService
            .BuildDuplicateGroups();

        var resultFiles =
            _duplicateService
                .AllFiles;

        var cleanup =
            _cleanupPipeline
                .Run(resultFiles);

        return new Package1ScanResult
        {
            Files =
                resultFiles,

            Cleanup =
                cleanup,

            TotalFiles =
                cleanup.TotalFiles,

            KeepCount =
                cleanup.KeepCount,

            DeleteCount =
                cleanup.DeleteCount,

            DuplicateGroups =
                _duplicateService
                    .DuplicateGroups
                    .Count,

            TotalBytes =
                cleanup.TotalBytes,

            BytesToDelete =
                cleanup.BytesToDelete,

            BytesToKeep =
                cleanup.BytesToKeep
        };
    }
}
using ITMartin.Media.Application.Models;
using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Pipelines;

public class Package1Pipeline
{
    private readonly Package1ScanPipeline
        _scanPipeline;

    private readonly Package1ExportPipeline
        _exportPipeline;

    public Package1Pipeline(
        Package1ScanPipeline scanPipeline,
        Package1ExportPipeline exportPipeline)
    {
        _scanPipeline =
            scanPipeline;

        _exportPipeline =
            exportPipeline;
    }

    // ====================================
    // SCAN
    // ====================================

    public async Task<Package1ScanResult>
        ScanAsync(
            string folderPath,
            Action<int, int, string>?
                progress = null)
    {
        return await _scanPipeline
            .RunAsync(
                folderPath,
                progress);
    }

    // ====================================
    // EXPORT
    // ====================================

    public async Task<Package1ExportResult>
        ExportAsync(
            IEnumerable<MediaFile> files,
            string exportRoot,
            Func<int, int, string, string, Task>?
                progress = null)
    {
        return await _exportPipeline
            .ExportAsync(
                files,
                exportRoot,
                progress);
    }
}
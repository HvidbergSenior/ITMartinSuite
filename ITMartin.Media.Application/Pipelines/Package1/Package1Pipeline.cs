using ITMartin.Media.Application.Models;
using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Pipelines.Package1;

public class Package1Pipeline
{
    private readonly Package1ScanPipeline
        _scanPipeline;

    private readonly ExportWorkflowStep
        _exportWorkflowStep;

    public Package1Pipeline(
        Package1ScanPipeline scanPipeline,
        ExportWorkflowStep exportWorkflowStep)
    {
        _scanPipeline =
            scanPipeline;

        _exportWorkflowStep =
            exportWorkflowStep;
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
        return await _exportWorkflowStep
            .ExportAsync(
                files,
                exportRoot,
                progress);
    }
}
using System.Diagnostics;
using ITMartin.Media.Application.Interfaces;
using ITMartin.Media.Application.Pipelines.QuickSort.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Entities;

namespace ITMartin.Media.Application.Pipelines.QuickSort.Steps;

public class QuickSortExportService
{
    private readonly ILibraryExportService
        _libraryExportService;

    public QuickSortExportService(
        ILibraryExportService
            libraryExportService)
    {
        _libraryExportService =
            libraryExportService;
    }

    public async Task<QuickSortExportResult>
        ExportAsync(
            IEnumerable<MediaFile> files,
            string exportRoot,
            Func<int, int, string, string, Task>?
                progress = null)
    {
        var stopwatch =
            Stopwatch.StartNew();

        try
        {
            var exportFiles =
                files
                    .Where(f =>
                        f.Status !=
                        MediaFileStatus.ToDelete)
                    .ToList();

            if (!exportFiles.Any())
            {
                return new QuickSortExportResult
                {
                    Success = true,
                    ExportRoot = exportRoot
                };
            }

            await _libraryExportService
                .ExportAsync(
                    exportFiles,
                    exportRoot,
                    progress);

            stopwatch.Stop();

            return new QuickSortExportResult
            {
                Success = true,

                ExportRoot =
                    exportRoot,

                ExportedFiles =
                    exportFiles.Count,

                ExportedBytes =
                    exportFiles.Sum(f =>
                        f.SizeBytes),

                Duration =
                    stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            return new QuickSortExportResult
            {
                Success = false,

                ExportRoot =
                    exportRoot,

                ErrorMessage =
                    ex.Message,

                Duration =
                    stopwatch.Elapsed
            };
        }
    }
}
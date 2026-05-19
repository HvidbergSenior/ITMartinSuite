using System.Diagnostics;
using ITMartin.Media.Application.Interfaces;
using ITMartin.Media.Application.Models;
using ITMartin.Media.Domain.Entities;
using ITMartin.Media.Enums;

namespace ITMartin.Media.Application.Pipelines;

public class Package1ExportPipeline
{
    private readonly ILibraryExportService
        _libraryExportService;

    public Package1ExportPipeline(
        ILibraryExportService
            libraryExportService)
    {
        _libraryExportService =
            libraryExportService;
    }

    public async Task<Package1ExportResult>
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
                return new Package1ExportResult
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

            return new Package1ExportResult
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

            return new Package1ExportResult
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
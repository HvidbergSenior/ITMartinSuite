using ITMartin.Media.Application.Models;
using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class ExportStatisticsProcessor
{
    public Package1ExportResult Build(
        IEnumerable<MediaFile> files,
        string exportRoot,
        TimeSpan duration)
    {
        var exportFiles =
            files.ToList();

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
                duration
        };
    }

    public Package1ExportResult Error(
        string exportRoot,
        Exception ex,
        TimeSpan duration)
    {
        return new Package1ExportResult
        {
            Success = false,

            ExportRoot =
                exportRoot,

            ErrorMessage =
                ex.Message,

            Duration =
                duration
        };
    }
}
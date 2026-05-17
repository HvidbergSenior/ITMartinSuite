using ITMartin.Media.Application.Models;
using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class FileScanResultProcessor
{
    public Package1ScanResult Build(
        List<MediaFile> files)
    {
        return new Package1ScanResult
        {
            Files =
                files,

            TotalFiles =
                files.Count,

            TotalBytes =
                files.Sum(f =>
                    f.SizeBytes)
        };
    }
}
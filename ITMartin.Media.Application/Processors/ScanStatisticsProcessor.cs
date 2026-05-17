using ITMartin.Media.Application.Models;
using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class ScanStatisticsProcessor
{
    public Package1ScanResult Build(
        List<MediaFile> files,
        Package1CleanupResult cleanup,
        int duplicateGroups)
    {
        return new Package1ScanResult
        {
            Files =
                files,

            Cleanup =
                cleanup,

            TotalFiles =
                cleanup.TotalFiles,

            KeepCount =
                cleanup.KeepCount,

            DeleteCount =
                cleanup.DeleteCount,

            DuplicateGroups =
                duplicateGroups,

            TotalBytes =
                cleanup.TotalBytes,

            BytesToDelete =
                cleanup.BytesToDelete,

            BytesToKeep =
                cleanup.BytesToKeep
        };
    }
}
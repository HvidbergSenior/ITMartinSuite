using ITMartin.Media.Application.Models;
using ITMartin.Media.Domain.Entities;
using ITMartin.Media.Enums;

namespace ITMartin.Media.Application.Pipelines;

public class Package1CleanupPipeline
{
    public Package1CleanupResult
        Run(
            IEnumerable<MediaFile> files)
    {
        var allFiles =
            files.ToList();

        var keepFiles =
            allFiles
                .Where(f =>
                    f.Status ==
                    MediaFileStatus.ToKeep)
                .ToList();

        var deleteFiles =
            allFiles
                .Where(f =>
                    f.Status ==
                    MediaFileStatus.ToDelete)
                .ToList();

        return new Package1CleanupResult
        {
            TotalFiles =
                allFiles.Count,

            KeepFiles =
                keepFiles,

            DeleteFiles =
                deleteFiles,

            KeepCount =
                keepFiles.Count,

            DeleteCount =
                deleteFiles.Count,

            TotalBytes =
                allFiles.Sum(f =>
                    f.SizeBytes),

            BytesToKeep =
                keepFiles.Sum(f =>
                    f.SizeBytes),

            BytesToDelete =
                deleteFiles.Sum(f =>
                    f.SizeBytes)
        };
    }
}
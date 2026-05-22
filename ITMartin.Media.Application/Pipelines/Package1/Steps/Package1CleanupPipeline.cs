using ITMartin.Media.Application.Pipelines.Package1.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Entities;

namespace ITMartin.Media.Application.Pipelines.Package1.Steps;

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
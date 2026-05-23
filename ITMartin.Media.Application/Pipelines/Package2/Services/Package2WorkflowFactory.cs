using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Requests;
using ITMartin.Media.Contracts.Contracts.Runtime.Requests.Package2;

namespace ITMartin.Media.Application.Pipelines.Package2.Services;

public sealed class Package2WorkflowFactory
{
    public Package2WorkflowState Create(
        Package1Manifest manifest,
        StartPackage2Request request)
    {
        var items =
            manifest.MediaFiles
                .Where(x =>
                    x.ExportedPath is not null)
                .Select(x =>
                    new EnhancedMediaItem
                    {
                        OriginalPath =
                            x.OriginalPath,

                        NormalizedPath =
                            x.NormalizedPath ??
                            x.ExportedPath!,

                        CurrentWorkingPath =
                            x.ExportedPath!,

                        MediaKind =
                            x.IsVideo
                                ? MediaKind.Video
                                : MediaKind.Image,

                        Operations =
                        [
                            new EnhancementOperation
                            {
                                Name = "Imported",
                                StartedAt = DateTimeOffset.UtcNow
                            }
                        ]
                    })
                .ToList();

        return new Package2WorkflowState
        {
            PackageId =
                Guid.NewGuid(),

            WorkingDirectory =
                Path.Combine(
                    request.SourceLibraryPath,
                    ".package2"),

            EnableAiEnhancement =
                request.EnableAiEnhancement,

            EnableUpscaling =
                request.EnableUpscaling,

            EnableFrameInterpolation =
                request.EnableFrameInterpolation,

            Items = items
        };
    }
}
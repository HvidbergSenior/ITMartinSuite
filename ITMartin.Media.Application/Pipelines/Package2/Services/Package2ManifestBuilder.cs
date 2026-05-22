using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Application.Pipelines.Package2.Services;

public sealed class Package2ManifestBuilder
{
    public Package2Manifest Build(
        Guid workflowId,
        Package2WorkflowState state)
    {
        return new Package2Manifest
        {
            WorkflowId =
                workflowId,

            PackageId =
                state.PackageId,

            CreatedAtUtc =
                DateTimeOffset.UtcNow,

            Profile =
                state.Profile,

            FileCount =
                state.Items.Count(x => !x.Failed),

            Items =
                state.Items
                    .Where(x =>
                        !x.Failed &&
                        x.CurrentWorkingPath is not null)
                    .Select(x =>
                        new EnhancedMediaManifestItem
                        {
                            OriginalPath =
                                x.OriginalPath,

                            NormalizedPath =
                                x.NormalizedPath,

                            EnhancedPath =
                                x.CurrentWorkingPath!,

                            MediaKind =
                                x.MediaKind,

                            Operations =
                                x.Operations.ToList()
                        })
                    .ToList()
        };
    }
}
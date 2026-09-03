using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Application.Pipelines.AnalogDigitize.Services;

public sealed class AnalogDigitizeManifestBuilder
{
    public AnalogDigitizeManifest Build(
        Guid workflowId,
        AnalogDigitizeWorkflowState state)
    {
        return new AnalogDigitizeManifest
        {
            WorkflowId =
                workflowId,

            PackageId =
                state.PackageId,

            CreatedAtUtc =
                DateTimeOffset.UtcNow,

            EnhancementProfile =
                state.EnhancementProfile,

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
                    .ToList(),
            RestorationProfile = state.RestorationProfile
        };
    }
}
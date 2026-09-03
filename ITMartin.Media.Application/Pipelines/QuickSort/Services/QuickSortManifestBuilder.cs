using ITMartin.Media.Application.Pipelines.QuickSort.Models;
using ITMartin.Media.Application.Pipelines.QuickSort.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Application.Pipelines.QuickSort.Services;

public sealed class QuickSortManifestBuilder
{
    public QuickSortManifest Build(
        Guid workflowId,
        QuickSortWorkflowState state)
    {
        return new QuickSortManifest
        {
            WorkflowId =
                workflowId,

            RootPath =
                state.RootPath,

            FileCount =
                state.MediaFiles.Count,

            MediaFiles =
                state.MediaFiles.ToList(),

            CreatedAtUtc =
                DateTimeOffset.UtcNow
        };
    }
}
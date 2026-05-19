using ITMartin.Media.Application.Pipelines.Package1.Models;
using ITMartin.Media.Application.Pipelines.Package1.Orchestration;

namespace ITMartin.Media.Application.Pipelines.Package1.Services;

public sealed class Package1ManifestBuilder
{
    public Package1Manifest Build(
        Guid workflowId,
        Package1WorkflowState state)
    {
        return new Package1Manifest
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
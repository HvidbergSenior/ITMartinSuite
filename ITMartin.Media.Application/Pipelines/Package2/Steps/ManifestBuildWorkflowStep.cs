using System.Text.Json;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class ManifestBuildWorkflowStep
    : Package2WorkflowStepBase
{
    public override string Name =>
        nameof(ManifestBuildWorkflowStep);

    public override async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
    {
        if (context.State is not Package2WorkflowState state)
        {
            return;
        }

        var manifestDirectory =
            Path.Combine(
                state.WorkingDirectory,
                "manifests");

        Directory.CreateDirectory(
            manifestDirectory);

        var manifestPath =
            Path.Combine(
                manifestDirectory,
                "package2-manifest.json");

        var json =
            JsonSerializer.Serialize(
                state,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

        await File.WriteAllTextAsync(
            manifestPath,
            json,
            cancellationToken);

        foreach (var item in state.Items
                     .Where(x =>
                         !x.Operations.Any(o =>
                             o.Name == Name &&
                             o.Success)))
        {
            item.Operations.Add(
                new EnhancementOperation
                {
                    Name = Name,
                    StartedAt =
                        DateTimeOffset.UtcNow,

                    CompletedAt =
                        DateTimeOffset.UtcNow,

                    Success = true,

                    Metadata = manifestPath
                });
        }
    }
}
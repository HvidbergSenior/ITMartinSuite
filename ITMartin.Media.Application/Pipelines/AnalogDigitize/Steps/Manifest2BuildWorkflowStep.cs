using System.Text.Json;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.AnalogDigitize.Steps;

public sealed class Manifest2BuildWorkflowStep
    : AnalogDigitizeWorkflowStepBase
{
    private readonly ILogger<
            Manifest2BuildWorkflowStep>
        _logger;

    public override string Name =>
        nameof(Manifest2BuildWorkflowStep);

    public Manifest2BuildWorkflowStep(
        ILogger<Manifest2BuildWorkflowStep> logger)
    {
        _logger =
            logger;
    }

    public override async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
    {
        if (context.State is not AnalogDigitizeWorkflowState state)
        {
            return;
        }

        await ExecuteOperationAsync(
            state.Items.First(),
            Name,
            async () =>
            {
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
                                 !AlreadyExecuted(
                                     x,
                                     Name)))
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
            },
            _logger);
    }
}
using System.Text.Json;
using ITMartin.Media.Application.Abstractions.Workflows;
using ITMartin.Media.Application.Processors;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Workflow.Steps;

public sealed class HashWorkflowStep : IWorkflowStep
{
    private readonly HashProcessor _processor;
private readonly ILogger<HashWorkflowStep> _logger;
    public HashWorkflowStep(
        HashProcessor processor, ILogger<HashWorkflowStep> logger)
    {
        _processor = processor;
        _logger = logger;
    }

    public string Name => "Hashing";

    public async Task ExecuteAsync(
        WorkflowExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        {
            cancellationToken.ThrowIfCancellationRequested();

            var files =
                context.Items["files"];
            var json = JsonSerializer.Serialize(files);
            _logger.LogInformation(
                "Files serialized successfully: {Length}",
                json.Length);
            await _processor.ProcessAsync(
                cancellationToken);

            context.Items["hashedFiles"] =
                files;
        }
    }
}
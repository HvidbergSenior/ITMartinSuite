
using System.Text.Json;
using ITMartin.Media.Application.Abstractions.Workflows;
using ITMartin.Media.Application.Processors;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Workflow.Steps;

public sealed class MetadataWorkflowStep : IWorkflowStep
{
    private readonly MetadataProcessor _processor;
private readonly ILogger<MetadataWorkflowStep> _logger;
    public MetadataWorkflowStep(
        MetadataProcessor processor, ILogger<MetadataWorkflowStep> logger)
    {
        _processor = processor;
        _logger = logger;
    }

    public string Name => "Metadata";
    public async Task ExecuteAsync(
        WorkflowExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        {
            cancellationToken.ThrowIfCancellationRequested();

            var files =
                context.Items["files"];

            await _processor.ProcessAsync(
                cancellationToken);
            var json = JsonSerializer.Serialize(files);

            _logger.LogInformation(
                "Files serialized successfully: {Length}",
                json.Length);
            context.Items["metadataFiles"] =
                files;
        }
    }
}
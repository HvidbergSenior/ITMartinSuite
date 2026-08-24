using ITMartin.Media.Application.Pipelines.Package1.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package1.Steps;

// Package1 is only ever a clean-start pass over genuine raw source material
// - incremental re-runs and partial fixes belong to Package3, not here. If
// the source folder is actually a copy of a library Package1 already sorted
// before (e.g. a backup pulled back off the NAS/an external HD), its own
// prior generated output would otherwise get ingested as if it were new
// content. Runs first, before FileDiscoveryWorkflowStep, so the rest of the
// pipeline only ever sees real source files - a no-op (nothing to delete)
// on truly raw input.
public sealed class CleanStartWorkflowStep : Package1WorkflowStepBase
{
    private static readonly string[] GeneratedFolders =
        ["_Galleri", "SmartFolders", ".package1", ".package2", ".package3", ".package4"];

    private static readonly string[] GeneratedFiles =
        ["manifest.json", "collections.json", "filestatus.json", "index.html"];

    private readonly ILogger<CleanStartWorkflowStep> _logger;

    public CleanStartWorkflowStep(ILogger<CleanStartWorkflowStep> logger)
    {
        _logger = logger;
    }

    public override string Name => "CleanStart";

    public override Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
        where TState : class
    {
        var state = context.State as Package1WorkflowState
            ?? throw new InvalidOperationException("Invalid workflow state");

        if (string.IsNullOrWhiteSpace(state.RootPath) || !Directory.Exists(state.RootPath))
            return Task.CompletedTask;

        var removedFolders = 0;
        foreach (var name in GeneratedFolders)
        {
            var path = Path.Combine(state.RootPath, name);
            if (!Directory.Exists(path)) continue;
            Directory.Delete(path, recursive: true);
            removedFolders++;
        }

        var removedFiles = 0;
        foreach (var name in GeneratedFiles)
        {
            var path = Path.Combine(state.RootPath, name);
            if (!File.Exists(path)) continue;
            File.Delete(path);
            removedFiles++;
        }

        if (removedFolders > 0 || removedFiles > 0)
        {
            _logger.LogInformation(
                "Clean start: removed {Folders} generated folder(s) and {Files} generated file(s) from {Root} before scanning",
                removedFolders, removedFiles, state.RootPath);
        }

        return Task.CompletedTask;
    }
}

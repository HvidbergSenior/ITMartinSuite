using ITMartin.Media.Application.Pipelines.Package1.Steps;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ManifestBuildWorkflowStep = ITMartin.Media.Application.Pipelines.Package2.Steps.ManifestBuildWorkflowStep;

namespace ITMartin.Media.Application.Pipelines.Package1.Orchestration;

public sealed class Package1WorkflowDefinition
    : IWorkflowDefinition
{
    public string Name =>
        "Package1Workflow";

    public IReadOnlyCollection<IWorkflowStep>
        Steps { get; }

    public Package1WorkflowDefinition(
        FileDiscoveryWorkflowStep fileDiscoveryWorkflowStep,
        HashWorkflowStep hashWorkflowStep,
        MetadataWorkflowStep metadataWorkflowStep,
        ImageNormalizationWorkflowStep imageNormalizationWorkflowStep,
        VideoNormalizationWorkflowStep videoNormalizationWorkflowStep,
        ThumbnailWorkflowStep thumbnailWorkflowStep,
        DuplicateDetectionWorkflowStep duplicateDetectionWorkflowStep,
        CleanupEvaluationWorkflowStep cleanupEvaluationWorkflowStep,
        ManifestBuildWorkflowStep manifestBuildWorkflowStep,
        ExportWorkflowExecutionStep exportWorkflowExecutionStep)
    {
        Steps =
        [
            // Discover input files
            fileDiscoveryWorkflowStep,

            // Create stable file identity
            hashWorkflowStep,

            // Read original metadata
            metadataWorkflowStep,

            // Normalize media
            imageNormalizationWorkflowStep,
            videoNormalizationWorkflowStep,

            // Generate derived assets
            thumbnailWorkflowStep,

            // Detect duplicates
            duplicateDetectionWorkflowStep,

            // Decide retention / cleanup
            cleanupEvaluationWorkflowStep,

            // Build internal manifest/package model
            manifestBuildWorkflowStep,

            // Write final exported structure
            exportWorkflowExecutionStep
        ];
    }
}
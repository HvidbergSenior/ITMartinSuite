using ITMartin.Media.Application.Pipelines.Package2.Steps;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Media.Application.Pipelines.Package2.Orchestration;

public sealed class Package2WorkflowDefinition
    : IWorkflowDefinition
{
    public string Name =>
        "Package2Workflow";

    public IReadOnlyCollection<IWorkflowStep>
        Steps { get; }

    public Package2WorkflowDefinition(
        RestorationPreparationWorkflowStep restorationPreparationWorkflowStep,
        ImageColorCorrectionWorkflowStep imageColorCorrectionWorkflowStep,
        ImageContrastWorkflowStep imageContrastWorkflowStep,
        ImageDenoiseWorkflowStep imageDenoiseWorkflowStep,
        ImageDeblurWorkflowStep imageDeblurWorkflowStep,
        ImageUpscaleWorkflowStep imageUpscaleWorkflowStep,
        VideoDeinterlaceWorkflowStep videoDeinterlaceWorkflowStep,
        VideoCropWorkflowStep videoCropWorkflowStep,
        
        VideoStabilizationWorkflowStep videoStabilizationWorkflowStep,
        VideoDenoiseWorkflowStep videoDenoiseWorkflowStep,
        VideoSharpenWorkflowStep videoSharpenWorkflowStep,
        VideoColorCorrectionWorkflowStep videoColorCorrectionWorkflowStep,
        VideoUpscaleWorkflowStep videoUpscaleWorkflowStep,

        AudioExtractionWorkflowStep audioExtractionWorkflowStep,
        AudioNoiseReductionWorkflowStep audioNoiseReductionWorkflowStep,
        AudioHumRemovalWorkflowStep audioHumRemovalWorkflowStep,
        AudioLevelingWorkflowStep audioLevelingWorkflowStep,
        AudioSpeechEnhancementWorkflowStep audioSpeechEnhancementWorkflowStep,
        AudioMuxWorkflowStep audioMuxWorkflowStep,

        CropDetectionWorkflowStep cropDetectionWorkflowStep,
        BorderRemovalWorkflowStep borderRemovalWorkflowStep,
        AspectRatioCorrectionWorkflowStep aspectRatioCorrectionWorkflowStep,

        QualityEvaluationWorkflowStep qualityEvaluationWorkflowStep,
        EnhancedThumbnailWorkflowStep enhancedThumbnailWorkflowStep,
        ManifestBuildWorkflowStep manifestBuildWorkflowStep,
        ExportEnhancedAssetsWorkflowStep exportEnhancedAssetsWorkflowStep)
    {
        Steps =
        [
            restorationPreparationWorkflowStep,

            imageColorCorrectionWorkflowStep,
            imageContrastWorkflowStep,
            imageDenoiseWorkflowStep,
            imageDeblurWorkflowStep,
            imageUpscaleWorkflowStep,

            videoDeinterlaceWorkflowStep,
            videoCropWorkflowStep,
            videoStabilizationWorkflowStep,
            videoDenoiseWorkflowStep,
            videoSharpenWorkflowStep,
            videoColorCorrectionWorkflowStep,
            videoUpscaleWorkflowStep,
            
            audioExtractionWorkflowStep,
            audioNoiseReductionWorkflowStep,
            audioHumRemovalWorkflowStep,
            audioLevelingWorkflowStep,
            audioSpeechEnhancementWorkflowStep,
            audioMuxWorkflowStep,

            cropDetectionWorkflowStep,
            borderRemovalWorkflowStep,
            aspectRatioCorrectionWorkflowStep,

            qualityEvaluationWorkflowStep,
            enhancedThumbnailWorkflowStep,
            manifestBuildWorkflowStep,
            exportEnhancedAssetsWorkflowStep
        ];
    }
}
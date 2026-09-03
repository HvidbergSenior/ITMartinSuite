using ITMartin.Media.Application.Pipelines.AnalogDigitize.Steps;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Media.Application.Pipelines.AnalogDigitize.Orchestration;

public sealed class AnalogDigitizeWorkflowDefinition
    : IWorkflowDefinition
{
    public string Name =>
        "AnalogDigitizeWorkflow";
    public WorkflowType WorkflowType =>
        WorkflowType.AnalogDigitize;
    public IReadOnlyCollection<IWorkflowStep>
        Steps { get; }

    public AnalogDigitizeWorkflowDefinition(
    RestorationPreparationWorkflowStep restorationPreparationWorkflowStep,
    // SAMPLE GENERATION
    VideoSampleGenerationWorkflowStep videoSampleGenerationWorkflowStep,

    // VIDEO
    VideoDeinterlaceWorkflowStep videoDeinterlaceWorkflowStep,
    VideoCropWorkflowStep videoCropWorkflowStep,
    VideoStabilizationWorkflowStep videoStabilizationWorkflowStep,
    VideoDenoiseWorkflowStep videoDenoiseWorkflowStep,
    VideoColorCorrectionWorkflowStep videoColorCorrectionWorkflowStep,
    VideoSharpenWorkflowStep videoSharpenWorkflowStep,
    VideoUpscaleWorkflowStep videoUpscaleWorkflowStep,

    // AUDIO
    AudioExtractionWorkflowStep audioExtractionWorkflowStep,
    AudioNoiseReductionWorkflowStep audioNoiseReductionWorkflowStep,
    AudioHumRemovalWorkflowStep audioHumRemovalWorkflowStep,
    AudioLevelingWorkflowStep audioLevelingWorkflowStep,
    AudioSpeechEnhancementWorkflowStep audioSpeechEnhancementWorkflowStep,

    // SINGLE RENDER
    VideoRenderWorkflowStep videoRenderWorkflowStep,

    // FINAL AUDIO MUX
    AudioMuxWorkflowStep audioMuxWorkflowStep,

    // OPTIONAL IMAGE
    ImageColorCorrectionWorkflowStep imageColorCorrectionWorkflowStep,
    ImageContrastWorkflowStep imageContrastWorkflowStep,
    ImageDenoiseWorkflowStep imageDenoiseWorkflowStep,
    ImageDeblurWorkflowStep imageDeblurWorkflowStep,
    ImageUpscaleWorkflowStep imageUpscaleWorkflowStep,

    // IMAGE CLEANUP
    CropDetectionWorkflowStep cropDetectionWorkflowStep,
    BorderRemovalWorkflowStep borderRemovalWorkflowStep,
    AspectRatioCorrectionWorkflowStep aspectRatioCorrectionWorkflowStep,

    // FINALIZATION
    QualityEvaluationWorkflowStep qualityEvaluationWorkflowStep,
    EnhancedThumbnailWorkflowStep enhancedThumbnailWorkflowStep,
    Manifest2BuildWorkflowStep manifest2BuildWorkflowStep,
    ExportEnhancedAssetsWorkflowStep exportEnhancedAssetsWorkflowStep)
{
    Steps =
    [
        // PREP
        restorationPreparationWorkflowStep,
        // SAMPLE GENERATION
        videoSampleGenerationWorkflowStep,

        // VIDEO FILTER REGISTRATION
        videoDeinterlaceWorkflowStep,
        videoCropWorkflowStep,
        videoStabilizationWorkflowStep,
        videoDenoiseWorkflowStep,
        videoColorCorrectionWorkflowStep,
        videoSharpenWorkflowStep,
        videoUpscaleWorkflowStep,

        // AUDIO FILTER REGISTRATION
        audioExtractionWorkflowStep,
        audioNoiseReductionWorkflowStep,
        audioHumRemovalWorkflowStep,
        audioLevelingWorkflowStep,
        audioSpeechEnhancementWorkflowStep,

        // SINGLE VIDEO/AUDIO RENDER
        videoRenderWorkflowStep,

        // FINAL AUDIO MUX
        audioMuxWorkflowStep,

        // OPTIONAL IMAGE PROCESSING
        imageColorCorrectionWorkflowStep,
        imageContrastWorkflowStep,
        imageDenoiseWorkflowStep,
        imageDeblurWorkflowStep,
        imageUpscaleWorkflowStep,

        // IMAGE CLEANUP
        cropDetectionWorkflowStep,
        borderRemovalWorkflowStep,
        aspectRatioCorrectionWorkflowStep,

        // FINALIZATION
        qualityEvaluationWorkflowStep,
        enhancedThumbnailWorkflowStep,
        manifest2BuildWorkflowStep,
        exportEnhancedAssetsWorkflowStep
    ];
}
}
using ITMartin.Media.Application.Pipelines.Package4.Steps;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Media.Application.Pipelines.Package4.Orchestration;

public sealed class Package4WorkflowDefinition : IWorkflowDefinition
{
    public string Name => "Package4Workflow";
    public WorkflowType WorkflowType => WorkflowType.Package4;
    public IReadOnlyCollection<IWorkflowStep> Steps { get; }

    public Package4WorkflowDefinition(
        SocialClipPreparationWorkflowStep preparationStep,

        // VIDEO / COLOR
        WhiteBalanceCorrectionWorkflowStep whiteBalanceStep,
        ExposureContrastCorrectionWorkflowStep exposureContrastStep,
        SaturationVibranceWorkflowStep saturationVibranceStep,
        ColorGradeWorkflowStep colorGradeStep,
        VideoSharpenWorkflowStep sharpenStep,
        VideoNoiseReductionWorkflowStep noiseReductionStep,
        DeflickerWorkflowStep deflickerStep,
        StabilizationWorkflowStep stabilizationStep,
        StabilizedCropWorkflowStep stabilizedCropStep,

        // AUDIO
        AudioNoiseReductionWorkflowStep audioNoiseReductionStep,
        WindNoiseReductionWorkflowStep windNoiseReductionStep,
        AudioHumRemovalWorkflowStep humRemovalStep,
        AudioEqWorkflowStep audioEqStep,
        DeEssWorkflowStep deEssStep,
        AudioCompressionWorkflowStep audioCompressionStep,
        LoudnessNormalizationWorkflowStep loudnessNormalizationStep,

        // RENDER + FINISHING
        VideoAudioRenderWorkflowStep renderStep,
        TrimDeadFootageWorkflowStep trimStep,
        DeliveryExportWorkflowStep deliveryExportStep)
    {
        Steps =
        [
            preparationStep,

            // Stabilization must run before color/audio filter registration -
            // it re-encodes to a new working file directly (its own 2-pass
            // ffmpeg process), whereas everything else here just appends
            // filter strings for the single combined render later.
            stabilizationStep,
            stabilizedCropStep,

            whiteBalanceStep,
            exposureContrastStep,
            saturationVibranceStep,
            colorGradeStep,
            noiseReductionStep,
            deflickerStep,
            sharpenStep,

            audioNoiseReductionStep,
            windNoiseReductionStep,
            humRemovalStep,
            audioEqStep,
            deEssStep,
            audioCompressionStep,
            loudnessNormalizationStep,

            renderStep,
            trimStep,
            deliveryExportStep
        ];
    }
}

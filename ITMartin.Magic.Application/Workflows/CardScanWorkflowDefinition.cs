using ITMartin.Magic.Application.Workflows.Steps;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Magic.Application.Workflows;

public sealed class CardScanWorkflowDefinition
    : IWorkflowDefinition
{
    public string Name =>
        "CardScanWorkflow";

    public IReadOnlyCollection<IWorkflowStep>
        Steps { get; }

    public CardScanWorkflowDefinition(
        DetectCardWorkflowStep detectCardWorkflowStep,
        DetectCardCornersWorkflowStep detectCardCornersWorkflowStep,
        PerspectiveCorrectionWorkflowStep perspectiveCorrectionWorkflowStep,
        BlurDetectionWorkflowStep blurDetectionWorkflowStep,
        OcrWorkflowStep ocrWorkflowStep,
        RecognitionWorkflowStep recognitionWorkflowStep,
        OpenAiRecognitionWorkflowStep openAiRecognitionWorkflowStep,
        ScryfallMatchWorkflowStep scryfallMatchWorkflowStep,
        ResultMappingWorkflowStep resultMappingWorkflowStep)
    {
        Steps =
        [
            // Computer Vision
            detectCardWorkflowStep,
            detectCardCornersWorkflowStep,
            perspectiveCorrectionWorkflowStep,
            blurDetectionWorkflowStep,

            // Text Extraction
            ocrWorkflowStep,

            // Semantic Interpretation
            recognitionWorkflowStep,
            openAiRecognitionWorkflowStep,

            // Domain Resolution
            scryfallMatchWorkflowStep,

            // Final Projection
            resultMappingWorkflowStep
        ];
    }
}
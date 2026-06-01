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
    public WorkflowType WorkflowType =>
        WorkflowType.MagicCardScan;
    public CardScanWorkflowDefinition(
        DetectCardWorkflowStep detectCardWorkflowStep,
        DetectCardCornersWorkflowStep detectCardCornersWorkflowStep,
        PerspectiveCorrectionWorkflowStep perspectiveCorrectionWorkflowStep,
        BlurDetectionWorkflowStep blurDetectionWorkflowStep,
        OcrWorkflowStep ocrWorkflowStep,
        OpenAiRecognitionWorkflowStep openAiRecognitionWorkflowStep)
        //ScryfallMatchWorkflowStep scryfallMatchWorkflowStep,
        //CardConditionWorkflowStep cardConditionWorkflowStep)
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
            openAiRecognitionWorkflowStep
            //scryfallMatchWorkflowStep,
            //cardConditionWorkflowStep
        ];
    }
}
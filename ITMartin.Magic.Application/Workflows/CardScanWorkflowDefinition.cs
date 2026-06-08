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
        OcrWorkflowStep ocrWorkflowStep,
        ScryfallMatchWorkflowStep scryfallMatchWorkflowStep,
        AiCardRecognitionWorkflowStep aiCardRecognitionWorkflowStep,
        CardConditionWorkflowStep cardConditionWorkflowStep,
        ResultMappingWorkflowStep resultMappingWorkflowStep,
        FinalScryfallMatchWorkflowStep finalScryfallMatchWorkflowStep)
    {
        Steps =
        [
            ocrWorkflowStep,

            scryfallMatchWorkflowStep,

            aiCardRecognitionWorkflowStep,

            scryfallMatchWorkflowStep,
            finalScryfallMatchWorkflowStep,
            
            cardConditionWorkflowStep,

            resultMappingWorkflowStep
        ];
    }
}
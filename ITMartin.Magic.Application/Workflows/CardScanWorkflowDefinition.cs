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
        AiCardRecognitionWorkflowStep aiCardRecognitionWorkflowStep,
        CardConditionWorkflowStep cardConditionWorkflowStep,
        ResultMappingWorkflowStep resultMappingWorkflowStep,
        FinalScryfallMatchWorkflowStep finalScryfallMatchWorkflowStep)
    {
        Steps =
        [
            aiCardRecognitionWorkflowStep,

            finalScryfallMatchWorkflowStep,

            cardConditionWorkflowStep,

            resultMappingWorkflowStep
        ];
    }
}
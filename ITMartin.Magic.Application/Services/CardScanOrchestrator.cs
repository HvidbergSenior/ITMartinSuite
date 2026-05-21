using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Application.Workflows;

namespace ITMartin.Magic.Application.Services;

public sealed class CardScanOrchestrator
    : ICardScanOrchestrator
{
    private readonly
        CardScanWorkflow
        _workflow;

    public CardScanOrchestrator(
        CardScanWorkflow workflow)
    {
        _workflow = workflow;
    }

    public async Task<CardScanContext> ExecuteAsync(
        string imagePath,
        CancellationToken cancellationToken)
    {
        var context =
            new CardScanContext
            {
                ImagePath = imagePath
            };

        await _workflow.ExecuteAsync(
            context,
            cancellationToken);

        return context;
    }
}
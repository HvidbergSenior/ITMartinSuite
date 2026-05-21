using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Application.Workflows.Steps;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Magic.Application.Workflows;

public sealed class ScryfallMatchWorkflowStep
    : IWorkflowStep
{
    private readonly
        IScryfallService
        _scryfallService;
    public string Name =>
        nameof(DetectCardWorkflowStep);

    public Task ExecuteAsync<TState>(WorkflowExecutionContext<TState> context, CancellationToken cancellationToken = default) where TState : class
    {
        throw new NotImplementedException();
    }

    public ScryfallMatchWorkflowStep(
        IScryfallService scryfallService)
    {
        _scryfallService =
            scryfallService;
    }

    public async Task ExecuteAsync(
        CardScanContext context,
        CancellationToken cancellationToken)
    {
        if (context.CaptureResult is null)
        {
            context.Fail(
                "Recognition result missing.");

            return;
        }

        var match =
            await _scryfallService
                .FindCardAsync(
                    context.CaptureResult);

        if (match is null)
        {
            context.Fail(
                "Scryfall match failed.");

            return;
        }

        context.CaptureResult =
            context.CaptureResult with
            {
                CardName = match.Name,
                SetCode = match.Set,
                CollectorNumber =
                match.CollectorNumber
            };
    }
}
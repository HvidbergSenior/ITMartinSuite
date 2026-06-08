using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Domain.Entities;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Magic.Application.Workflows.Steps;

public sealed class UpdateCollectionWorkflowStep
    : WorkflowStep<CardScanContext>
{
    private readonly
        IMagicCardRepository
        _repository;

    public override string Name =>
        nameof(UpdateCollectionWorkflowStep);

    public UpdateCollectionWorkflowStep(
        IMagicCardRepository repository)
    {
        _repository =
            repository;
    }

    public override async Task ExecuteAsync(
        WorkflowExecutionContext<CardScanContext> context,
        CancellationToken cancellationToken = default)
    {
        var match =
            context.State.ScryfallMatchResult;
        
        if (context.State.ScryfallMatchResult is null)
        {
            return;
        }
        var cards =
            await _repository.GetAllAsync(
                cancellationToken);

        var existing =
            cards.FirstOrDefault(
                x =>
                    x.SetCode ==
                    match.SetCode
                    && x.CollectorNumber ==
                    match.CollectorNumber);

        if (existing is null)
        {
            var card =
                new MagicCard
                {
                    Id =
                        Guid.NewGuid(),

                    Name =
                        match.Name,

                    SetCode =
                        match.SetCode,

                    CollectorNumber =
                        match.CollectorNumber,

                    ScryfallId =
                        match.ScryfallId,

                    Quantity =
                        1,

                    FirstSeenAt =
                        DateTime.UtcNow,

                    LastSeenAt =
                        DateTime.UtcNow
                };

            await _repository.AddAsync(
                card,
                cancellationToken);

            return;
        }

        existing.Quantity++;

        existing.LastSeenAt =
            DateTime.UtcNow;

        await _repository.UpdateAsync(
            existing,
            cancellationToken);
    }
}
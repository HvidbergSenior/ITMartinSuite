using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Domain.Entities;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Magic.Application.Workflows.Steps;

public sealed class SaveMagicCardWorkflowStep
    : WorkflowStep<CardScanContext>
{
    private readonly
        IMagicCardScanRepository
        _repository;

    public override string Name =>
        nameof(SaveMagicCardWorkflowStep);

    public SaveMagicCardWorkflowStep(
        IMagicCardScanRepository repository)
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
        if (match is null)
        {
            return;
        }
        var condition =
            context.State.ConditionResult;

        var scan =
            new MagicCardScan
            {
                Id =
                    Guid.NewGuid(),

                OriginalImagePath =
                    context.State.ImagePath,

                CardName =
                    match?.Name,

                SetCode =
                    match?.SetCode,

                CollectorNumber =
                    match?.CollectorNumber,

                ScryfallId =
                    match?.ScryfallId,

                ImageUrl =
                    match?.ImageUrl,

                EurPrice =
                    match?.EurPrice,

                UsdPrice =
                    match?.UsdPrice,

                Condition =
                    condition?.Condition,

                CreatedAt =
                    DateTime.UtcNow,
                
            };

        await _repository.SaveAsync(
            scan,
            cancellationToken);
    }
}
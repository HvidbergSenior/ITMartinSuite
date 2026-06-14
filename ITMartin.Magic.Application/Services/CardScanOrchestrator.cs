using System.Text.Json;
using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Application.Workflows;
using ITMartin.Media.Application.Abstractions.BackgroundJobs;
using ITMartin.Media.Application.Abstractions.BackgroundJobs.Models;

namespace ITMartin.Magic.Application.Services;

public sealed class CardScanOrchestrator
    : ICardScanOrchestrator
{
    private readonly
        CardScanWorkflowRunner
        _workflow;

    private readonly IBackgroundJobQueue _queue;

    public CardScanOrchestrator(
        CardScanWorkflowRunner workflow,
        IBackgroundJobQueue queue)
    {
        _workflow = workflow;
        _queue = queue;
    }

    public async Task<CardScanContext> ExecuteAsync(
        string imagePath,
        string? setCode,
        CancellationToken cancellationToken)
    {
        var payload =
            JsonSerializer.Serialize(
                new CardScanJobPayload(
                    imagePath,
                    setCode));

        await _queue.EnqueueAsync(
            new BackgroundJob
            {
                Id = Guid.NewGuid(),
                Queue = "workflow",
                Type = BackgroundJobTypes.ProcessMedia,
                Payload = payload,
                CreatedAt = DateTimeOffset.UtcNow,
                Status = "Pending"
            },
            cancellationToken);

        return new CardScanContext
        {
            ImagePath = imagePath
        };
    }
}
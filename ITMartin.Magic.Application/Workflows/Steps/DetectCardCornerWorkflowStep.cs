using System.Text.Json;
using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Application.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Magic.Application.Workflows.Steps;

public sealed class DetectCardCornersWorkflowStep
    : WorkflowStep<CardScanContext>
{
    private readonly
        ICardCornerDetectionService
        _cardCornerDetectionService;

    public override string Name =>
        nameof(DetectCardCornersWorkflowStep);

    public DetectCardCornersWorkflowStep(
        ICardCornerDetectionService cardCornerDetectionService)
    {
        _cardCornerDetectionService =
            cardCornerDetectionService;
    }

    public override async Task ExecuteAsync(
        WorkflowExecutionContext<CardScanContext> context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            context.State.DetectedCardImagePath);

        var result =
            await _cardCornerDetectionService
                .DetectAsync(
                    context.State.DetectedCardImagePath,
                    cancellationToken);

        Console.WriteLine(
            $"Corner Result Null: {result is null}");
        Console.WriteLine(
            $"TL={result?.TopLeft?.X},{result?.TopLeft?.Y}");

        Console.WriteLine(
            $"TR={result?.TopRight?.X},{result?.TopRight?.Y}");

        Console.WriteLine(
            $"BR={result?.BottomRight?.X},{result?.BottomRight?.Y}");

        Console.WriteLine(
            $"BL={result?.BottomLeft?.X},{result?.BottomLeft?.Y}");
        
        if (result is null)
        {
            throw new InvalidOperationException(
                "Card contour not found.");
        }
        
        Console.WriteLine(
            System.Text.Json.JsonSerializer.Serialize(
                result,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                }));

        context.State.CardCornerResult =
            result ??
            new CardCornerDetectionResult();
    }
}
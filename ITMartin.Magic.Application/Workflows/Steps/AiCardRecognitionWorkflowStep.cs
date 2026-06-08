using System.Text.Json;
using ITMartin.Ai.Interfaces;
using ITMartin.Ai.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Magic.Application.Workflows.Steps;

public sealed class AiCardRecognitionWorkflowStep
    : WorkflowStep<CardScanContext>
{
    private readonly IMagicCardRecognitionService
        _magicCardRecognitionService;

    public override string Name =>
        nameof(AiCardRecognitionWorkflowStep);

    public AiCardRecognitionWorkflowStep(
        IMagicCardRecognitionService magicCardRecognitionService)
    {
        _magicCardRecognitionService =
            magicCardRecognitionService;
    }

    public override async Task ExecuteAsync(
        WorkflowExecutionContext<CardScanContext> context,
        CancellationToken cancellationToken = default)
    {
        if (context.State.HasConfirmedMatch)
        {
            return;
        }

        var imagePath =
            context.State.DetectedCardImagePath
            ?? context.State.ImagePath;

        var detectionResult =
            context.State.DetectionResult
            ?? new CardDetectionResult();

        var result =
            await _magicCardRecognitionService
                .AnalyzeAsync(
                    imagePath,
                    detectionResult,
                    cancellationToken);

        if (result is null)
        {
            throw new InvalidOperationException(
                "AI recognition returned null.");
        }

        context.State.OpenAiResult =
            result;

        context.State.CardName =
            result.Name;

        context.State.CollectorNumber =
            result.CollectorNumber;

        context.State.IdentificationConfidence =
            result.Confidence;

        Console.WriteLine(
            $"OPENAI RESULT: {JsonSerializer.Serialize(result)}");

        Console.WriteLine(
            $"NAME: [{result.Name}]");

        Console.WriteLine(
            $"ARTIST: [{result.Artist}]");

        Console.WriteLine(
            $"COLLECTOR: [{result.CollectorNumber}]");

        Console.WriteLine(
            $"COPYRIGHT: [{result.CopyrightYear}]");

        Console.WriteLine(
            $"WHITE BORDER: [{result.WhiteBorder}]");

        Console.WriteLine(
            $"OLD BORDER: [{result.OldBorder}]");

        Console.WriteLine(
            $"SYMBOL VISIBLE: [{result.SetSymbolVisible}]");

        Console.WriteLine(
            $"SYMBOL DESCRIPTION: [{result.VisibleSetSymbolDescription}]");

        Console.WriteLine(
            $"RARITY: [{result.Rarity}]");

        Console.WriteLine(
            $"CONFIDENCE: [{result.Confidence}]");
    }
}
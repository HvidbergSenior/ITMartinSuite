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

        var result =
            await _magicCardRecognitionService
                .AnalyzeAsync(
                    imagePath,
                    cancellationToken);

        if (result is null)
        {
            throw new InvalidOperationException(
                "AI recognition returned null.");
        }

        result.IdentificationConfidence =
            CalculateConfidence(result);

        context.State.OpenAiResult =
            result;

        context.State.OpenAiResult =
            result;

        context.State.CardName =
            result.IdentifiedName;

        context.State.CollectorNumber =
            result.CollectorNumber;

        context.State.IdentificationConfidence =
            result.IdentificationConfidence;

        Console.WriteLine(
            $"OPENAI RESULT: {JsonSerializer.Serialize(result)}");

        Console.WriteLine(
            $"IDENTIFIED CARD: [{result.IdentifiedName}]");

        Console.WriteLine(
            $"CONFIDENCE: [{result.IdentificationConfidence}]");

        Console.WriteLine(
            $"ARTIST: [{result.Artist}]");

        Console.WriteLine(
            $"COLLECTOR: [{result.CollectorNumber}]");

    }
    private static decimal CalculateConfidence(
        MagicCardAnalysisResult result)
    {
        if (string.IsNullOrWhiteSpace(
                result.IdentifiedName))
        {
            return 0m;
        }

        var score = 0.5m;

        if (!string.IsNullOrWhiteSpace(result.ManaCost))
            score += 0.1m;

        if (!string.IsNullOrWhiteSpace(result.CardType))
            score += 0.1m;

        if (!string.IsNullOrWhiteSpace(result.Artist))
            score += 0.1m;

        if (!string.IsNullOrWhiteSpace(result.CollectorNumber))
            score += 0.1m;

        return Math.Min(score, 1.0m);
    }
}
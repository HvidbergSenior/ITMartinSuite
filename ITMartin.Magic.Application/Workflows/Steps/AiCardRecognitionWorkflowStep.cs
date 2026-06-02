using System.Text.Json;
using ITMartin.Ai.Interfaces;
using ITMartin.Ai.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Magic.Application.Workflows.Steps;

public sealed class AiCardRecognitionWorkflowStep
    : WorkflowStep<CardScanContext>
{
    private readonly
        IMagicCardRecognitionService
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
        var imagePath =
            context.State.PerspectiveCorrectedImagePath
            ?? context.State.DetectedCardImagePath
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

        context.State.FrameType =
            result.OldBorder
                ? MagicCardFrameType.OldBorder
                : result.WhiteBorder
                    ? MagicCardFrameType.WhiteBorder
                    : MagicCardFrameType.Modern;

        Console.WriteLine(
            $"FRAME TYPE: [{context.State.FrameType}]");

        Console.WriteLine(
            $"OPENAI RESULT: {JsonSerializer.Serialize(result)}");

        Console.WriteLine(
            $"OPENAI NAME: [{result.Name}]");

        Console.WriteLine(
            $"OPENAI SET: [{result.SetCode}]");

        Console.WriteLine(
            $"OPENAI COLLECTOR: [{result.CollectorNumber}]");
    }
}
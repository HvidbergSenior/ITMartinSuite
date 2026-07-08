using ITMartin.Ai.Interfaces;
using ITMartin.Ai.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Magic.Application.Workflows.Steps;

public sealed class AiCardRecognitionWorkflowStep
    : WorkflowStep<CardScanContext>
{
    private readonly IMagicCardRecognitionService
        _magicCardRecognitionService;

    private readonly ILogger<AiCardRecognitionWorkflowStep> _logger;

    public override string Name =>
        nameof(AiCardRecognitionWorkflowStep);

    public AiCardRecognitionWorkflowStep(
        IMagicCardRecognitionService magicCardRecognitionService,
        ILogger<AiCardRecognitionWorkflowStep> logger)
    {
        _magicCardRecognitionService =
            magicCardRecognitionService;
        _logger = logger;
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

        context.State.AiResult =
            result;

        // Mode mismatch: "no set symbol" was selected but the card actually has one -
        // proceeding would silently search only the small no-symbol whitelist (Alpha
        // through 4th/5th Edition) and likely land on a wrong reprint rather than the
        // card's real (expansion-set) printing. Fail clearly instead of guessing.
        if (string.IsNullOrWhiteSpace(context.State.SetCode) &&
            result.HasVisibleSetSymbol == true)
        {
            context.State.Fail(
                $"Dette kort ser ud til at have et sætsymbol — det passer ikke med \"Kort uden sætsymbol\". Prøv \"Vælg sæt\" i stedet{(string.IsNullOrWhiteSpace(result.IdentifiedName) ? "" : $" for \"{result.IdentifiedName}\"")}.");
            return;
        }

        context.State.CardName =
            result.IdentifiedName;

        context.State.CollectorNumber =
            result.CollectorNumber;

        context.State.IdentificationConfidence =
            result.IdentificationConfidence;

        _logger.LogInformation(
            "AI result — Card: [{Name}] Confidence: [{Confidence}] Artist: [{Artist}] Collector: [{Collector}]",
            result.IdentifiedName,
            result.IdentificationConfidence,
            result.Artist,
            result.CollectorNumber);

    }
}
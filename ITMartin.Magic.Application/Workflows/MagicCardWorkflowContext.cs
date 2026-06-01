using ITMartin.Ai.Models;
using ITMartin.Magic.Application.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.OCR.Models;

namespace ITMartin.Magic.Application.Workflows;

public sealed class MagicCardWorkflowContext
{
    public required MediaFile MediaFile { get; init; }

    public CardDetectionResult? DetectionResult { get; set; }

    public OcrResult? OcrResult { get; set; }

    public MagicCardAnalysisResult? OpenAiResult { get; set; }

    public ScryfallMatchResult? ScryfallMatchResult { get; set; }
}
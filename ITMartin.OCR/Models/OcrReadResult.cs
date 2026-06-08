namespace ITMartin.OCR.Models;

public sealed record OcrReadResult(
    string? Text,
    float Confidence);
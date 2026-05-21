namespace ITMartin.Magic.Contracts.Scan.Models;

public sealed record CardRecognitionResult(
    string? CardName,
    string? SetCode,
    string? CollectorNumber,
    double Confidence);
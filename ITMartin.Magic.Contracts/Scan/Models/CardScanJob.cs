using ITMartin.Magic.Contracts.Scan.Enums;

namespace ITMartin.Magic.Contracts.Scan.Models;

public sealed record CardScanJob(
    Guid Id,
    Guid SessionId,
    string ImagePath,
    ScanJobStatus Status,
    CardRecognitionResult? Result);
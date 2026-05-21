namespace ITMartin.Magic.Contracts.Scan.Requests;

public sealed record EnqueueCardScanRequest(
    Guid SessionId,
    string ImagePath);
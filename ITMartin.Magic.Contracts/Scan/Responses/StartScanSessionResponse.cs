namespace ITMartin.Magic.Contracts.Scan.Requests;

public sealed record StartScanSessionRequest(
    string DeviceId,
    string Mode);
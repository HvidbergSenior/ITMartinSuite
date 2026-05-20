namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public sealed record StartScanRequest(
    string RootPath,
    bool Recursive,
    bool GenerateThumbnails,
    bool EnableAiProcessing,
    string PackageName);
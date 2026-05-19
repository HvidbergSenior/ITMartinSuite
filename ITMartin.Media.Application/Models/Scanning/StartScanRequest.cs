namespace ITMartin.Media.Application.Models.Scanning;

public sealed record StartScanRequest(
    string RootPath,
    bool Recursive,
    bool GenerateThumbnails,
    bool EnableAiProcessing,
    string PackageName);
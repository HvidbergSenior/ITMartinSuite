namespace ITMartin.Media.Application.CQRS.Commands.StartScan;

public sealed record StartScanCommand(
    string RootPath,
    bool Recursive);
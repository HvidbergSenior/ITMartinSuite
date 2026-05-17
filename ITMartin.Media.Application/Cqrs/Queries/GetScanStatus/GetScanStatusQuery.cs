namespace ITMartin.Media.Application.CQRS.Queries.GetScanStatus;

public sealed record GetScanStatusQuery(
    Guid SessionId);
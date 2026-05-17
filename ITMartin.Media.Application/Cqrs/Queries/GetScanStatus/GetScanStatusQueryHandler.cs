using ITMartin.Media.Application.Abstractions.Scanning;
using ITMartin.Media.Application.Models.Scanning;
using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.CQRS.Queries.GetScanStatus;

public sealed class GetScanStatusQueryHandler
    : IQueryHandler<GetScanStatusQuery, ScanSession?>
{
    private readonly IScanSessionRepository _repository;

    public GetScanStatusQueryHandler(
        IScanSessionRepository repository)
    {
        _repository = repository;
    }

    public Task<ScanSession?> HandleAsync(
        GetScanStatusQuery query,
        CancellationToken cancellationToken)
    {
        return _repository.GetAsync(
            query.SessionId,
            cancellationToken);
    }
}
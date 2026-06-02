using ITMartin.Magic.Domain.Entities;

namespace ITMartin.Magic.Application.Interfaces;

public interface IMagicCardScanRepository
{
    Task SaveAsync(
        MagicCardScan scan,
        CancellationToken cancellationToken = default);
}
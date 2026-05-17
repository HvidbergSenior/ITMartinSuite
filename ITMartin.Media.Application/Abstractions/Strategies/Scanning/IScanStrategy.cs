using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Abstractions.Strategies.Scanning;

public interface IScanStrategy
{
    string Name { get; }

    Task<IReadOnlyCollection<MediaFile>>
        ExecuteAsync(
            string path,
            CancellationToken cancellationToken);
}
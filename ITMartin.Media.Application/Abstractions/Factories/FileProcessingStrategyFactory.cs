using ITMartin.Media.Application.Abstractions.Factories;
using ITMartin.Media.Application.Abstractions.Strategies;

namespace ITMartin.Media.Infrastructure.Factories;

public sealed class FileProcessingStrategyFactory : IFileProcessingStrategyFactory
{
    private readonly IEnumerable<IFileProcessingStrategy> _strategies;

    public FileProcessingStrategyFactory(
        IEnumerable<IFileProcessingStrategy> strategies)
    {
        _strategies = strategies;
    }

    public IFileProcessingStrategy Resolve(string extension)
    {
        return _strategies.First(x => x.CanHandle(extension));
    }
}
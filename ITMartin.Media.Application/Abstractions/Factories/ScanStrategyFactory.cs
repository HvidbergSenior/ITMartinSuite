using ITMartin.Media.Application.Abstractions.Strategies.Scanning;

namespace ITMartin.Media.Application.Abstractions.Factories;

public sealed class ScanStrategyFactory
{
    private readonly IEnumerable<IScanStrategy>
        _strategies;

    public ScanStrategyFactory(
        IEnumerable<IScanStrategy> strategies)
    {
        _strategies = strategies;
    }

    public IScanStrategy Resolve(
        string name)
    {
        return _strategies.First(x =>
            x.Name.Equals(
                name,
                StringComparison.OrdinalIgnoreCase));
    }
}
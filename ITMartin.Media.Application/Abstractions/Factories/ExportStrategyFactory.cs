using ITMartin.Media.Application.Abstractions.Strategies.Export;

namespace ITMartin.Media.Application.Abstractions.Factories;

public sealed class ExportStrategyFactory
{
    private readonly IEnumerable<IExportStrategy>
        _strategies;

    public ExportStrategyFactory(
        IEnumerable<IExportStrategy> strategies)
    {
        _strategies = strategies;
    }

    public IExportStrategy Resolve(
        string name)
    {
        return _strategies.First(x =>
            x.Name.Equals(
                name,
                StringComparison.OrdinalIgnoreCase));
    }
}
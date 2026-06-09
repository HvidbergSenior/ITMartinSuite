public interface ISetSymbolMatchingService
{
    Task<decimal> MatchAsync(
        string observedSymbol,
        string setCode,
        string setName,
        CancellationToken cancellationToken);
}
using ITMartin.Magic.Application.Interfaces;

namespace ITMartin.Magic.Infrastructure.Services;

public sealed class SetSymbolMatchingService
    : ISetSymbolMatchingService
{
    public Task<decimal> MatchAsync(
        string? observedSymbol,
        string setCode,
        string setName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(
                observedSymbol))
        {
            return Task.FromResult(0m);
        }

        return Task.FromResult(0m);
    }
}
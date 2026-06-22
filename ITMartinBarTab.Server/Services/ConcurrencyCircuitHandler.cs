using Microsoft.AspNetCore.Components.Server.Circuits;

namespace ITMartinBarTab.Server.Services;

public sealed class ConcurrencyCircuitHandler(ConcurrencyService concurrency) : CircuitHandler
{
    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken ct)
    {
        concurrency.Increment();
        return Task.CompletedTask;
    }

    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken ct)
    {
        concurrency.Decrement();
        return Task.CompletedTask;
    }
}

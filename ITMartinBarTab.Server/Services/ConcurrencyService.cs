namespace ITMartinBarTab.Server.Services;

public sealed class ConcurrencyService
{
    private int _active;

    public int Active => _active;

    public void Increment() => Interlocked.Increment(ref _active);
    public void Decrement() => Interlocked.Decrement(ref _active);
}

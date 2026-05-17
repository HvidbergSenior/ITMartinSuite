namespace ITMartin.Media.Application.Processors;

public class ScanCancellationProcessor
{
    private readonly CancellationTokenSource
        _cts = new();

    public CancellationToken Token =>
        _cts.Token;

    public void Cancel()
    {
        _cts.Cancel();
    }
}
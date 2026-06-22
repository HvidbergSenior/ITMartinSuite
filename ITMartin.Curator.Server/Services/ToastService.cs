namespace ITMartin.Curator.Server.Services;

public sealed class ToastService
{
    public event Action<string, string>? OnShow;

    public void Show(string message, string type = "info") =>
        OnShow?.Invoke(message, type);
}

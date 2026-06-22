namespace ITMartinAdhd.Server.Services;

public sealed class ToastService
{
    private readonly List<ToastMessage> _toasts = [];
    public IReadOnlyList<ToastMessage> Toasts => _toasts;
    public event Action? OnChange;

    public void Show(string text, string type = "success")
    {
        var toast = new ToastMessage(Guid.NewGuid(), text, type);
        _toasts.Add(toast);
        OnChange?.Invoke();
        _ = Task.Delay(3000).ContinueWith(_ =>
        {
            _toasts.Remove(toast);
            OnChange?.Invoke();
        });
    }
}

public sealed record ToastMessage(Guid Id, string Text, string Type);

using Microsoft.JSInterop;

namespace ITMartinR6Assistant.Server.Services;

// Per-device player name - scoped (one instance per Blazor Server circuit,
// i.e. per connected browser tab), backed by localStorage so it survives a
// reload on that same device. No login, no password - just a name so this
// device's choices (loadout overrides, pre-game check submissions) can be
// attributed to a player for the team overview.
public class PlayerIdentityService
{
    private const string StorageKey = "r6assistant_player_name";
    private readonly IJSRuntime _js;
    private Task? _loadTask;

    public PlayerIdentityService(IJSRuntime js) => _js = js;

    public string? Name { get; private set; }

    public event Action? OnChanged;

    // Multiple components (layout + page) call this independently on the
    // same scoped instance during the same first render. A bool guard would
    // let the second caller return immediately while the first is still
    // mid-await, so it'd render with a stale null Name and never get told
    // when the real value shows up. Caching the task itself means every
    // caller awaits the exact same completion, race-free.
    public Task EnsureLoadedAsync() => _loadTask ??= LoadAsync();

    private async Task LoadAsync()
    {
        var stored = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        Name = string.IsNullOrWhiteSpace(stored) ? null : stored;
        OnChanged?.Invoke();
    }

    public async Task SetNameAsync(string? name)
    {
        Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        if (Name is null)
            await _js.InvokeVoidAsync("localStorage.removeItem", StorageKey);
        else
            await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, Name);
        OnChanged?.Invoke();
    }
}

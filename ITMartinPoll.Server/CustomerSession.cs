using ITMartinPoll.Application;
using Microsoft.JSInterop;

namespace ITMartinPoll.Server;

// The customer's kundenummer for this visit (one per browser tab, Blazor circuit). Read from the phone ONCE, as soon as
// Stem opens (MainLayout), and checked with Kunder - so "Lav ny" shows the form at once instead of three pictures in a
// row (user 2026-10-21: "pushing lav ny gives a flicker"). UI state only; the rules stay in CustomerPolls.
public sealed class CustomerSession(CustomerPolls polls, IJSRuntime js)
{
    public const string Key = "stem-kundenummer";

    public string Code { get; private set; } = "";
    /// <summary>The number on this phone has been read (and checked when there was one).</summary>
    public bool Ready { get; private set; }
    public bool LoggedIn { get; private set; }
    public List<CustomerPolls.Mine> Mine { get; private set; } = [];
    public event Action? Changed;

    private Task? _start;

    /// <summary>Safe to call from every page - the phone is asked only once.</summary>
    public Task StartAsync() => _start ??= ReadAsync();

    private async Task ReadAsync()
    {
        string code = "";
        try { code = await js.InvokeAsync<string?>("localStorage.getItem", Key) ?? ""; } catch { }
        if (code != "") await LoginAsync(code);
        Ready = true;
        Changed?.Invoke();
    }

    /// <returns>Null when logged in, else the error for the person.</returns>
    public async Task<string?> LoginAsync(string code)
    {
        var (ok, error) = await polls.LoginAsync(code);
        if (!ok) { LoggedIn = false; return error; }
        (Code, LoggedIn) = (code, true);
        try { await js.InvokeVoidAsync("localStorage.setItem", Key, code); } catch { }
        await RefreshAsync();
        return null;
    }

    public async Task RefreshAsync() { if (LoggedIn) Mine = await polls.MineAsync(Code); }

    public async Task LogoutAsync()
    {
        try { await js.InvokeVoidAsync("localStorage.removeItem", Key); } catch { }
        (Code, LoggedIn, Mine) = ("", false, []);
        Changed?.Invoke();
    }
}

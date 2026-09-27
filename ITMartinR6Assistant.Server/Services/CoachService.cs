using ITMartinR6Assistant.Infrastructure;
using Microsoft.JSInterop;

namespace ITMartinR6Assistant.Server.Services;

// Per-circuit view of coach mode (user: "I as admin/coach control the phases and what to do and show - other players
// see what I choose"). A device id in localStorage identifies this browser, so the coach stays coach after a reload.
// Claiming needs R6:CoachPin (falls back to R6:UpdatePin); claiming while someone else is coach takes over.
public class CoachService
{
    private const string DeviceKey = "r6assistant_device";
    private readonly IJSRuntime _js;
    private readonly IConfiguration _config;
    private readonly SessionStateService _session;
    private readonly PlayerIdentityService _identity;
    private Task? _loadTask;
    private string? _deviceId;

    public CoachService(IJSRuntime js, IConfiguration config, SessionStateService session, PlayerIdentityService identity)
    {
        _js = js; _config = config; _session = session; _identity = identity;
    }

    public bool IsCoach => _deviceId is not null && _session.CoachId == _deviceId;
    // Someone is coach: the phase screen follows the coach's choices.
    public bool Synced => _session.CoachId is not null;
    public bool CanControl => !Synced || IsCoach;

    public Task EnsureLoadedAsync() => _loadTask ??= LoadAsync();

    private async Task LoadAsync()
    {
        _deviceId = await _js.InvokeAsync<string?>("localStorage.getItem", DeviceKey);
        if (string.IsNullOrEmpty(_deviceId))
        {
            _deviceId = Guid.NewGuid().ToString("N");
            await _js.InvokeVoidAsync("localStorage.setItem", DeviceKey, _deviceId);
        }
    }

    public bool TryClaim(string pin)
    {
        var expected = _config["R6:CoachPin"] is { Length: > 0 } p ? p : _config["R6:UpdatePin"];
        if (_deviceId is null || string.IsNullOrEmpty(expected) || pin.Trim() != expected) return false;
        _session.SetCoach(_deviceId, _identity.Name ?? "Coach");
        return true;
    }

    public void Release()
    {
        if (IsCoach) _session.SetCoach(null, null);
    }
}

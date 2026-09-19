using System.Text.Json;
using Microsoft.JSInterop;
using ITMartinElPriser.Core;

namespace ITMartinElPriser.Server.Services;

// The free app keeps nothing about you on the server. Region and tariff
// presets live in a cookie on your own phone: read from the request that
// opened the Blazor circuit, written back through a tiny JS helper.
public sealed class PrefsService(IHttpContextAccessor http, IJSRuntime js)
{
    public const string CookieName = "ep_prefs";
    private HouseholdSettings? _settings;

    public HouseholdSettings Get() => _settings ??= ReadCookie(http.HttpContext);

    public async Task SaveAsync(HouseholdSettings s)
    {
        _settings = Scrub(s);
        var json = JsonSerializer.Serialize(_settings);
        await js.InvokeVoidAsync("elprefs.save", CookieName, Uri.EscapeDataString(json));
    }

    public static HouseholdSettings ReadCookie(HttpContext? http)
    {
        var raw = http?.Request.Cookies[CookieName];
        if (string.IsNullOrEmpty(raw)) return new HouseholdSettings();
        try { return Scrub(JsonSerializer.Deserialize<HouseholdSettings>(Uri.UnescapeDataString(raw)) ?? new HouseholdSettings()); }
        catch { return new HouseholdSettings(); }
    }

    // Never carry meter details in the free app, whatever the cookie says.
    private static HouseholdSettings Scrub(HouseholdSettings s)
    {
        s.EloverblikToken = "";
        s.MeteringPointId = "";
        s.MeteringPointAddress = "";
        return s;
    }

    // Short label for the header: "DK1 · N1 · NRGi Time".
    public static string Describe(HouseholdSettings s)
    {
        if (!s.ShowAllIn) return $"Spotpris · {s.PriceArea}";
        var grid = GridCompanyPreset.All.FirstOrDefault(g => g.Id == s.GridCompanyId)?.Name ?? "eget net";
        var sup = SupplierPreset.All.FirstOrDefault(p => p.Id == s.SupplierId)?.Name ?? "eget tillæg";
        return $"{s.PriceArea} · {grid} · {sup}";
    }
}

using System.Text.Json;
using ITMartinDailyBrief.Domain;
using Microsoft.JSInterop;

namespace ITMartinDailyBrief.Server;

// The reader's choice lives in their own browser (localStorage), under the same keys as always:
// brief_max_items, brief_source_<id> = true/false, brief_custom_sources = [{Id, Name, Url}].
public static class BrowserChoice
{
    private sealed record Custom(string Id, string Name, string Url);

    public static async Task<ReaderChoice> LoadAsync(IJSRuntime js)
    {
        var max = int.TryParse(await Get(js, "brief_max_items"), out var n) ? n : (int?)null;
        List<NewsSource> custom = [];
        try
        {
            if (await Get(js, "brief_custom_sources") is { Length: > 0 } json)
                custom = (JsonSerializer.Deserialize<List<Custom>>(json) ?? []).Select(c => new NewsSource(c.Id, c.Name, c.Url)).ToList();
        }
        catch (JsonException) { }

        var switches = new Dictionary<string, bool>();
        foreach (var s in NewsSource.Presets.Concat(custom))
            if (await Get(js, $"brief_source_{s.Id}") is { } raw) switches[s.Id] = raw == "true";
        return new ReaderChoice(max, switches, custom);
    }

    public static async Task SaveAsync(IJSRuntime js, ReaderChoice choice)
    {
        await js.InvokeVoidAsync("localStorage.setItem", "brief_max_items", choice.MaxItems.ToString());
        foreach (var (id, on) in choice.Switches)
            await js.InvokeVoidAsync("localStorage.setItem", $"brief_source_{id}", on ? "true" : "false");
        await js.InvokeVoidAsync("localStorage.setItem", "brief_custom_sources",
            JsonSerializer.Serialize(choice.CustomSources.Select(c => new Custom(c.Id, c.Name, c.RssUrl))));
    }

    private static async Task<string?> Get(IJSRuntime js, string key) => await js.InvokeAsync<string?>("localStorage.getItem", key);
}

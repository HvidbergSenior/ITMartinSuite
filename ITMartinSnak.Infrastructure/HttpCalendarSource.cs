using ITMartinSnak.Application;

namespace ITMartinSnak.Infrastructure;

/// <summary>Reads a public .ics calendar over https. At most 2 MB and 10 seconds; anything else counts as "not reachable".</summary>
public sealed class HttpCalendarSource : ICalendarSource
{
    private const int MaxBytes = 2 * 1024 * 1024;
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public async Task<string?> FetchAsync(string url, CancellationToken ct)
    {
        using var res = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!res.IsSuccessStatusCode) return null;
        if (res.Content.Headers.ContentLength > MaxBytes) return null;
        await using var s = await res.Content.ReadAsStreamAsync(ct);
        using var ms = new MemoryStream();
        var buf = new byte[81920];
        int n;
        while ((n = await s.ReadAsync(buf, ct)) > 0)
        {
            ms.Write(buf, 0, n);
            if (ms.Length > MaxBytes) return null;
        }
        var text = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        return text.Contains("BEGIN:VCALENDAR") ? text : null;
    }
}

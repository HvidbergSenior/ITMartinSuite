using System.Globalization;
using System.Text.Json;
using FalsigForening.Application;
using FalsigForening.Domain;

namespace FalsigForening.Infrastructure.External;

// cvrapi.dk: free, public company details, wants an identifying User-Agent (the same API Jacob's Foreningsmakker uses).
// The contact person ("addressco") is never read - personal data we do not need.
public sealed class CvrApiLookup(HttpClient http) : ICvrLookup
{
    private const string UserAgent = "ITMartin Forening - itmartin.dk";

    public async Task<CvrDetails?> LookupAsync(string numberOrName, CancellationToken ct = default)
    {
        var number = ForeningRules.CleanCvr(numberOrName);
        var url = "https://cvrapi.dk/api?" + (number.Length > 0 ? "vat=" + number : "search=" + Uri.EscapeDataString(numberOrName.Trim())) + "&country=dk";
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd(UserAgent);
            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return null;
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            return Parse(await JsonDocument.ParseAsync(stream, cancellationToken: ct));
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException) { return null; }
    }

    internal static CvrDetails? Parse(JsonDocument doc)
    {
        var d = doc.RootElement;
        if (d.ValueKind != JsonValueKind.Object || d.TryGetProperty("error", out _) || !d.TryGetProperty("vat", out var vat)) return null;
        string? Text(string name) => d.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.String or JsonValueKind.Number ? v.ToString() : null;
        return new CvrDetails(
            vat.ToString(), Text("name") ?? "", Text("address") ?? "", Text("zipcode") ?? "", Text("city") ?? "",
            DanishDate(Text("startdate")), Text("companydesc"), Text("email"),
            !string.IsNullOrEmpty(Text("enddate")));
    }

    // cvrapi gives "01/08 - 1974"
    internal static DateOnly? DanishDate(string? text) =>
        DateOnly.TryParseExact((text ?? "").Replace(" ", ""), "dd/MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
}

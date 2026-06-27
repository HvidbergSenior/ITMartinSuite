using System.Net.Http.Json;
using System.Text.Json;
using ITMartinSuite.Maui.Models;

namespace ITMartinSuite.Maui.Services;

public class FamilieApiService
{
    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public FamilieApiService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<FamilyTaskDto>> GetTodayAsync(CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<List<FamilyTaskDto>>(
            "api/familie/today", _json, ct);
        return result ?? [];
    }

    public async Task<FamilyTaskDto?> CreateTaskAsync(
        string type,
        string? note,
        Stream? photoStream,
        string? photoFileName,
        string createdBy,
        CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(type), "type");
        form.Add(new StringContent(createdBy), "createdBy");

        if (!string.IsNullOrEmpty(note))
            form.Add(new StringContent(note), "note");

        if (photoStream is not null && photoFileName is not null)
            form.Add(new StreamContent(photoStream), "photo", photoFileName);

        var response = await _http.PostAsync("api/familie/tasks", form, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<FamilyTaskDto>(_json, ct);
    }

    public async Task ClaimAsync(Guid id, string claimedBy, CancellationToken ct = default)
    {
        await _http.PutAsJsonAsync(
            $"api/familie/tasks/{id}/claim",
            new { ClaimedBy = claimedBy },
            ct);
    }

    public async Task CompleteAsync(Guid id, CancellationToken ct = default)
    {
        await _http.PutAsync($"api/familie/tasks/{id}/complete", null, ct);
    }

    public string GetPhotoUrl(string? photoPath) =>
        string.IsNullOrEmpty(photoPath)
            ? string.Empty
            : $"{_http.BaseAddress}api/familie/photos/{photoPath}";
}

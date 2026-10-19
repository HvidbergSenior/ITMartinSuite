using System.Text.Json;
using ITMartinUpload.Domain;
using Microsoft.Extensions.Options;

namespace ITMartinUpload.Server.Services;

/// <summary>Reads FileSorter's _kilder.json from the customer's library (mounted read-only) and pairs it with the
/// uploaded files through <see cref="SortOverview"/>. A missing or broken file just means "nothing sorted yet".</summary>
public sealed class SortStatusReader(IOptions<UploadOptions> options, UploadStore store)
{
    public const string FileName = "_kilder.json";

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public (IReadOnlyList<FileSortStatus> Files, SortSummary Summary) For(UploadCustomer customer)
    {
        var report = ReadReport(customer);
        var files = SortOverview.Build(store.List(customer.Slug).Select(f => new UploadedFile(f.Name, f.ModifiedUtc)), report);
        return (files, SortOverview.Summarize(files, report));
    }

    private SortReport? ReadReport(UploadCustomer customer)
    {
        var library = string.IsNullOrWhiteSpace(customer.Library) ? customer.Slug : customer.Library;
        if (!UploadStore.IsValidSlug(library)) return null;
        var path = Path.Combine(options.Value.LibraryRoot, library, FileName);
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<SortReport>(File.ReadAllText(path), Json) : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}

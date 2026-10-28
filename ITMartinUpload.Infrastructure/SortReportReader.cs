using System.Text.Json;
using ITMartinUpload.Application;
using ITMartinUpload.Domain;
using Microsoft.Extensions.Options;

namespace ITMartinUpload.Infrastructure;

/// <summary>Reads FileSorter's _kilder.json from the customer's library (mounted read-only). A missing or broken file
/// just means "nothing sorted yet".</summary>
public sealed class SortReportReader(IOptions<StorageOptions> options) : ISortReports
{
    public const string FileName = "_kilder.json";

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public SortReport? Read(string library)
    {
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

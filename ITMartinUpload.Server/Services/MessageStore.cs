using System.Text.Json;

namespace ITMartinUpload.Server.Services;

public sealed record ChatMessage(string From, string Text, DateTime AtUtc)
{
    public const string Customer = "kunde";
    public const string Martin = "martin";

    public bool IsFromMartin => From == Martin;
}

/// <summary>
/// The conversation with one customer, kept as a json file next to their uploads.
/// A thread is a handful of short messages, so a file per customer is enough and
/// keeps everything about a customer in one folder - easy to hand over or delete.
/// </summary>
public sealed class MessageStore
{
    private const string FileName = "_beskeder.json";
    private const int MaxTextLength = 2000;

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private readonly object _lock = new();
    private readonly UploadStore _uploads;

    public MessageStore(UploadStore uploads) => _uploads = uploads;

    private string PathFor(string slug) => Path.Combine(_uploads.FolderFor(slug), FileName);

    public IReadOnlyList<ChatMessage> Read(string slug)
    {
        if (!UploadStore.IsValidSlug(slug)) return [];
        lock (_lock)
        {
            var path = PathFor(slug);
            if (!File.Exists(path)) return [];
            try
            {
                return JsonSerializer.Deserialize<List<ChatMessage>>(File.ReadAllText(path)) ?? [];
            }
            catch (JsonException) { return []; }
            catch (IOException) { return []; }
        }
    }

    /// <summary>Appends a message; returns false when there is nothing worth storing.</summary>
    public bool Add(string slug, string from, string text)
    {
        if (!UploadStore.IsValidSlug(slug)) return false;

        text = (text ?? "").Trim();
        if (text.Length == 0) return false;
        if (text.Length > MaxTextLength) text = text[..MaxTextLength];

        lock (_lock)
        {
            var all = Read(slug).ToList();
            all.Add(new ChatMessage(from, text, DateTime.UtcNow));

            var folder = _uploads.FolderFor(slug);
            Directory.CreateDirectory(folder);
            File.WriteAllText(PathFor(slug), JsonSerializer.Serialize(all, Json));
        }
        return true;
    }

    /// <summary>Messages from the customer that Martin has not answered yet.</summary>
    public int UnansweredCount(string slug)
    {
        var all = Read(slug);
        var lastFromMartin = all.FindLastIndex(m => m.IsFromMartin);
        return all.Count - (lastFromMartin + 1);
    }

    public DateTime? LastActivityUtc(string slug) =>
        Read(slug) is { Count: > 0 } all ? all[^1].AtUtc : null;
}

file static class ListExtensions
{
    public static int FindLastIndex<T>(this IReadOnlyList<T> list, Func<T, bool> match)
    {
        for (var i = list.Count - 1; i >= 0; i--)
            if (match(list[i])) return i;
        return -1;
    }
}

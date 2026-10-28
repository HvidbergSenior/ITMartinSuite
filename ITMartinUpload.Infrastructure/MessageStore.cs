using System.Text.Json;
using ITMartinUpload.Application;
using ITMartinUpload.Domain;

namespace ITMartinUpload.Infrastructure;

/// <summary>
/// The conversation with one customer, kept as a json file next to their uploads.
/// A thread is a handful of short messages, so a file per customer is enough and
/// keeps everything about a customer in one folder - easy to hand over or delete.
/// </summary>
public sealed class MessageStore(UploadStore uploads) : IMessageStore
{
    private const string FileName = "_beskeder.json";

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private readonly object _lock = new();

    private string PathFor(string slug) => Path.Combine(uploads.FolderFor(slug), FileName);

    public IReadOnlyList<ChatMessage> Read(string slug)
    {
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

    public void Append(string slug, ChatMessage message)
    {
        lock (_lock)
        {
            var all = Read(slug).ToList();
            all.Add(message);
            Directory.CreateDirectory(uploads.FolderFor(slug));
            File.WriteAllText(PathFor(slug), JsonSerializer.Serialize(all, Json));
        }
    }
}

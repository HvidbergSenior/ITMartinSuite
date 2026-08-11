using System.Collections.Concurrent;
using System.Text.Json;

namespace ITMartinLiveGallery.Server;

// Same idiom as ITMartinLive.Server's LiveService - a flat JSON file instead
// of a real database, since this is a small, short-lived (one event at a
// time, deleted after) dataset, not something that needs migrations or
// query performance. Two files instead of Live's one because photos grow
// unbounded per event while events themselves are few - keeping them
// separate avoids rewriting the whole photo list every time an event's
// metadata changes (it doesn't, in practice, but the pattern is cheap).
public sealed class GalleryService
{
    private readonly string _eventsFile;
    private readonly string _photosFile;
    private readonly ConcurrentDictionary<string, LiveEventInfo> _events = new();
    private readonly ConcurrentDictionary<string, List<EventPhoto>> _photos = new();
    private readonly object _saveLock = new();

    public GalleryService(IConfiguration config)
    {
        var dataDir = config["Gallery:DataDir"] ?? "/data";
        Directory.CreateDirectory(dataDir);
        _eventsFile = Path.Combine(dataDir, "events.json");
        _photosFile = Path.Combine(dataDir, "photos.json");
        Load();
    }

    private void Load()
    {
        if (File.Exists(_eventsFile))
        {
            var events = JsonSerializer.Deserialize<List<LiveEventInfo>>(File.ReadAllText(_eventsFile)) ?? [];
            foreach (var e in events) _events[e.Slug] = e;
        }
        if (File.Exists(_photosFile))
        {
            var photos = JsonSerializer.Deserialize<List<EventPhoto>>(File.ReadAllText(_photosFile)) ?? [];
            foreach (var group in photos.GroupBy(p => p.Slug))
                _photos[group.Key] = group.OrderBy(p => p.UploadedAt).ToList();
        }
    }

    private void SaveEvents()
    {
        lock (_saveLock)
            File.WriteAllText(_eventsFile,
                JsonSerializer.Serialize(_events.Values.ToList(), new JsonSerializerOptions { WriteIndented = true }));
    }

    private void SavePhotos()
    {
        lock (_saveLock)
            File.WriteAllText(_photosFile,
                JsonSerializer.Serialize(_photos.Values.SelectMany(p => p).ToList(), new JsonSerializerOptions { WriteIndented = true }));
    }

    public LiveEventInfo CreateEvent(string slug, string pin, string title)
    {
        var ev = new LiveEventInfo { Slug = slug, Pin = pin, Title = title, CreatedAt = DateTime.UtcNow };
        _events[slug] = ev;
        SaveEvents();
        return ev;
    }

    public LiveEventInfo? GetEvent(string slug) => _events.GetValueOrDefault(slug);

    public bool ValidatePin(string slug, string? pin) =>
        _events.TryGetValue(slug, out var ev) && ev.Pin == pin;

    public List<LiveEventInfo> AllEvents() => _events.Values.OrderByDescending(e => e.CreatedAt).ToList();

    public EventPhoto AddPhoto(string slug, string filename, string thumbFilename, bool isVideo, string? uploaderName)
    {
        var photo = new EventPhoto
        {
            Slug = slug,
            Filename = filename,
            ThumbFilename = thumbFilename,
            IsVideo = isVideo,
            UploaderName = string.IsNullOrWhiteSpace(uploaderName) ? null : uploaderName.Trim(),
            UploadedAt = DateTime.UtcNow,
        };
        _photos.AddOrUpdate(slug,
            _ => [photo],
            (_, list) => { list.Add(photo); return list; });
        SavePhotos();
        return photo;
    }

    public List<EventPhoto> GetPhotos(string slug) =>
        _photos.TryGetValue(slug, out var list) ? list.OrderByDescending(p => p.UploadedAt).ToList() : [];

    public bool DeleteEvent(string slug)
    {
        var removed = _events.TryRemove(slug, out _);
        _photos.TryRemove(slug, out _);
        if (removed) { SaveEvents(); SavePhotos(); }
        return removed;
    }
}

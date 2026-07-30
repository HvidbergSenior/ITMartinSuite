using System.Collections.Concurrent;
using System.Text.Json;
using ITMartinLive.Server.Models;

namespace ITMartinLive.Server.Services;

public class LiveService
{
    private readonly ConcurrentDictionary<string, LiveEvent> _events = new();
    private readonly string _dataFile = "/data/events.json";
    private readonly ILogger<LiveService> _logger;

    public string AdminPin { get; }

    public event Action<string>? Changed;
    private void Notify(string slug) => Changed?.Invoke(slug);

    public LiveService(ILogger<LiveService> logger, IConfiguration config)
    {
        _logger  = logger;
        AdminPin = config["Live:AdminPin"] ?? "live2025";
        Load();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_dataFile)) return;
            var json   = File.ReadAllText(_dataFile);
            var events = JsonSerializer.Deserialize<List<LiveEvent>>(json);
            if (events is null) return;
            foreach (var ev in events)
                _events[ev.Slug] = ev;
            _logger.LogInformation("Loaded {Count} events from disk", events.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load events from disk");
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_dataFile)!);
            File.WriteAllText(_dataFile,
                JsonSerializer.Serialize(_events.Values.ToList(),
                    new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not save events to disk");
        }
    }

    public LiveEvent? Get(string slug) =>
        _events.TryGetValue(slug.ToLowerInvariant(), out var e) ? e : null;

    public IReadOnlyList<LiveEvent> GetActive() =>
        [.. _events.Values.Where(e => e.IsActive).OrderByDescending(e => e.CreatedAt)];

    public IReadOnlyList<LiveEvent> GetAll() =>
        [.. _events.Values.OrderByDescending(e => e.CreatedAt)];

    public LiveEvent Create(string name, string sportEmoji, string slug, string writerPin)
    {
        slug = slug.ToLowerInvariant().Trim();
        var ev = new LiveEvent { Slug = slug, Name = name, SportEmoji = sportEmoji, WriterPin = writerPin };
        _events[slug] = ev;
        Save();
        Notify(slug);
        return ev;
    }

    public void AddUpdate(string slug, LiveUpdate update)
    {
        var ev = Get(slug);
        if (ev is null) return;
        lock (ev.Updates) ev.Updates.Insert(0, update);
        Save();
        Notify(slug);
    }

    public void UpdateHeader(string slug, string text)
    {
        var ev = Get(slug);
        if (ev is null) return;
        ev.HeaderText = text;
        Save();
        Notify(slug);
    }

    public void React(string slug, Guid updateId, string emoji)
    {
        var ev = Get(slug);
        var u = ev?.Updates.FirstOrDefault(x => x.Id == updateId);
        if (u is null) return;
        lock (u.Reactions) { if (u.Reactions.ContainsKey(emoji)) u.Reactions[emoji]++; }
        Notify(slug);
    }

    public void VotePoll(string slug, Guid updateId, int idx)
    {
        var ev = Get(slug);
        var u = ev?.Updates.FirstOrDefault(x => x.Id == updateId);
        if (u is null || idx >= u.PollOptions.Count) return;
        u.PollOptions[idx].Votes++;
        Notify(slug);
    }

    public void ToggleStar(string slug, Guid updateId)
    {
        var ev = Get(slug);
        var u = ev?.Updates.FirstOrDefault(x => x.Id == updateId);
        if (u is null) return;
        u.IsStarred = !u.IsStarred;
        Save();
        Notify(slug);
    }

    public void DeleteUpdate(string slug, Guid updateId)
    {
        var ev = Get(slug);
        if (ev is null) return;
        lock (ev.Updates)
        {
            var u = ev.Updates.FirstOrDefault(x => x.Id == updateId);
            if (u is not null) ev.Updates.Remove(u);
        }
        Save();
        Notify(slug);
    }

    public void SubmitMessage(string slug, ViewerMessage msg)
    {
        var ev = Get(slug);
        if (ev is null) return;
        lock (ev.PendingMessages) ev.PendingMessages.Add(msg);
        Notify(slug);
    }

    public void ApproveMessage(string slug, Guid msgId)
    {
        var ev = Get(slug);
        if (ev is null) return;
        ViewerMessage? msg;
        lock (ev.PendingMessages)
        {
            msg = ev.PendingMessages.FirstOrDefault(m => m.Id == msgId);
            if (msg is not null) ev.PendingMessages.Remove(msg);
        }
        if (msg is null) return;
        AddUpdate(slug, new LiveUpdate { Type = UpdateType.Text, Text = $"💬 {msg.Author}: {msg.Text}" });
    }

    public void RejectMessage(string slug, Guid msgId)
    {
        var ev = Get(slug);
        if (ev is null) return;
        lock (ev.PendingMessages)
        {
            var m = ev.PendingMessages.FirstOrDefault(x => x.Id == msgId);
            if (m is not null) ev.PendingMessages.Remove(m);
        }
        Notify(slug);
    }

    public void ToggleActive(string slug)
    {
        var ev = Get(slug);
        if (ev is null) return;
        ev.IsActive = !ev.IsActive;
        Save();
        Notify(slug);
    }

    public void JoinViewer(string slug)
    {
        var ev = Get(slug);
        if (ev is not null) { ev.ViewerCount++; Notify(slug); }
    }

    public void LeaveViewer(string slug)
    {
        var ev = Get(slug);
        if (ev is not null) { ev.ViewerCount = Math.Max(0, ev.ViewerCount - 1); Notify(slug); }
    }
}

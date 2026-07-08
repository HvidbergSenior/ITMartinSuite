using System.Collections.Concurrent;
using System.Text.Json;
using ITMartinStream.Server.Models;

namespace ITMartinStream.Server.Services;

public class StreamService
{
    private readonly ConcurrentDictionary<string, StreamProject> _projects = new();
    private readonly string _dataFile = "/data/projects.json";
    private readonly ILogger<StreamService> _logger;

    public string AdminPin { get; }

    public StreamService(ILogger<StreamService> logger, IConfiguration config)
    {
        _logger = logger;
        AdminPin = config["Stream:AdminPin"] ?? "stream2025";
        Load();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_dataFile)) return;
            var json = File.ReadAllText(_dataFile);
            var projects = JsonSerializer.Deserialize<List<StreamProject>>(json);
            if (projects is null) return;
            foreach (var p in projects) _projects[p.Slug] = p;
            _logger.LogInformation("Loaded {Count} projects from disk", projects.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load projects from disk");
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_dataFile)!);
            File.WriteAllText(_dataFile,
                JsonSerializer.Serialize(_projects.Values.ToList(),
                    new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not save projects to disk");
        }
    }

    public StreamProject? Get(string slug) =>
        _projects.TryGetValue(slug.ToLowerInvariant(), out var p) ? p : null;

    public IReadOnlyList<StreamProject> GetActive() =>
        [.. _projects.Values.Where(p => p.IsActive).OrderByDescending(p => p.CreatedAt)];

    public IReadOnlyList<StreamProject> GetAll() =>
        [.. _projects.Values.OrderByDescending(p => p.CreatedAt)];

    public StreamProject Create(string name, string emoji, string slug, string writerPin)
    {
        slug = slug.ToLowerInvariant().Trim();
        var p = new StreamProject { Slug = slug, Name = name, Emoji = emoji, WriterPin = writerPin };
        _projects[slug] = p;
        Save();
        return p;
    }

    public void AddUpdate(string slug, StreamUpdate update)
    {
        var p = Get(slug);
        if (p is null) return;
        lock (p.Updates) p.Updates.Insert(0, update);
        Save();
    }

    public void UpdateStatus(string slug, string text)
    {
        var p = Get(slug);
        if (p is null) return;
        p.StatusText = text;
        Save();
    }

    public void UpdateStreamUrl(string slug, string? url)
    {
        var p = Get(slug);
        if (p is null) return;
        p.StreamUrl = string.IsNullOrWhiteSpace(url) ? null : url.Trim();
        Save();
    }

    public void React(string slug, Guid updateId, string emoji)
    {
        var p = Get(slug);
        var u = p?.Updates.FirstOrDefault(x => x.Id == updateId);
        if (u is null) return;
        lock (u.Reactions) { if (u.Reactions.ContainsKey(emoji)) u.Reactions[emoji]++; }
        Save();
    }

    public void VotePoll(string slug, Guid updateId, int idx)
    {
        var p = Get(slug);
        var u = p?.Updates.FirstOrDefault(x => x.Id == updateId);
        if (u is null || idx >= u.PollOptions.Count) return;
        u.PollOptions[idx].Votes++;
        Save();
    }

    public void ToggleStar(string slug, Guid updateId)
    {
        var p = Get(slug);
        var u = p?.Updates.FirstOrDefault(x => x.Id == updateId);
        if (u is null) return;
        u.IsStarred = !u.IsStarred;
        Save();
    }

    public void DeleteUpdate(string slug, Guid updateId)
    {
        var p = Get(slug);
        if (p is null) return;
        lock (p.Updates)
        {
            var u = p.Updates.FirstOrDefault(x => x.Id == updateId);
            if (u is not null) p.Updates.Remove(u);
        }
        Save();
    }

    public void SubmitComment(string slug, Comment comment)
    {
        var p = Get(slug);
        if (p is null) return;
        lock (p.PendingComments) p.PendingComments.Add(comment);
        Save();
    }

    public void ApproveComment(string slug, Guid commentId)
    {
        var p = Get(slug);
        if (p is null) return;
        Comment? c;
        lock (p.PendingComments)
        {
            c = p.PendingComments.FirstOrDefault(m => m.Id == commentId);
            if (c is not null) p.PendingComments.Remove(c);
        }
        if (c is null) return;
        AddUpdate(slug, new StreamUpdate { Type = UpdateType.Text, Text = $"💬 {c.Author}: {c.Text}" });
    }

    public void RejectComment(string slug, Guid commentId)
    {
        var p = Get(slug);
        if (p is null) return;
        lock (p.PendingComments)
        {
            var c = p.PendingComments.FirstOrDefault(x => x.Id == commentId);
            if (c is not null) p.PendingComments.Remove(c);
        }
        Save();
    }

    public void ToggleActive(string slug)
    {
        var p = Get(slug);
        if (p is null) return;
        p.IsActive = !p.IsActive;
        Save();
    }
}

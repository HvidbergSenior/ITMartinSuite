using System.Collections.Concurrent;
using ITMartinDailyBrief.Domain;
using Microsoft.Extensions.Logging;

namespace ITMartinDailyBrief.Application;

// Reads one RSS/Atom source. Returns [] when the source cannot be read.
public interface IFeedReader
{
    Task<IReadOnlyList<NewsItem>> ReadAsync(NewsSource source, CancellationToken ct);
}

// Writes a short Danish summary of one article ("" = could not).
public interface ISummaryWriter
{
    Task<string> SummarizeAsync(NewsItem item, CancellationToken ct);
}

// The front page and single articles. Each source is read at most once a minute, whoever asks;
// every item seen is remembered (by id) so an article page can be opened from any reader's front page.
public sealed class NewsFeed(IFeedReader reader, TimeProvider clock)
{
    public static readonly TimeSpan SourceCacheTtl = TimeSpan.FromMinutes(1);
    private const int RememberMax = 2000;

    private readonly ConcurrentDictionary<string, (DateTime At, IReadOnlyList<NewsItem> Items)> _perSource = new();
    private readonly ConcurrentDictionary<string, NewsItem> _seen = new();

    public DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<List<NewsItem>> FrontPageAsync(ReaderChoice choice, CancellationToken ct = default)
    {
        var lists = await Task.WhenAll(choice.ActiveSources.Select(s => SourceAsync(s, ct)));
        return NewsItem.FrontPage(lists, choice.MaxItems);
    }

    public NewsItem? Find(string id) => _seen.GetValueOrDefault(id);

    private async Task<IReadOnlyList<NewsItem>> SourceAsync(NewsSource source, CancellationToken ct)
    {
        var key = source.RssUrl;
        if (_perSource.TryGetValue(key, out var hit) && Now - hit.At < SourceCacheTtl) return hit.Items;
        var items = await reader.ReadAsync(source, ct);
        _perSource[key] = (Now, items);
        if (_seen.Count > RememberMax) _seen.Clear();
        foreach (var i in items) _seen[i.Id] = i;
        return items;
    }
}

// AI summary of one article, on request. Each article is summarised once for everybody (kept in memory),
// and the whole app has a hard daily ceiling (News:AiPerDay, default 200).
public sealed class Summaries(ISummaryWriter writer, SummaryBudget budget, TimeProvider clock, ILogger<Summaries> log)
{
    public enum Outcome { Ok, NoArticle, DayLimit, Failed }
    public sealed record Result(Outcome Outcome, string Text = "");

    private readonly ConcurrentDictionary<string, string> _done = new();
    private readonly object _lock = new();

    public string? Known(string id) => _done.GetValueOrDefault(id);

    public async Task<Result> SummarizeAsync(NewsItem? item, CancellationToken ct = default)
    {
        if (item is null) return new(Outcome.NoArticle);
        if (_done.TryGetValue(item.Id, out var cached)) return new(Outcome.Ok, cached);
        lock (_lock)
            if (!budget.TryUse(clock.GetUtcNow().UtcDateTime))
            {
                log.LogWarning("AI summary day limit ({Max}) reached", budget.MaxPerDay);
                return new(Outcome.DayLimit);
            }
        var text = await writer.SummarizeAsync(item, ct);
        if (text.Length == 0) return new(Outcome.Failed);
        _done[item.Id] = text;
        return new(Outcome.Ok, text);
    }
}

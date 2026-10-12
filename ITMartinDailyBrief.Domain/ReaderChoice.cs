namespace ITMartinDailyBrief.Domain;

// What one reader has chosen: how many news, which sources (presets on/off + their own RSS addresses).
// Kept in the reader's own browser - nothing about the reader is stored on the server.
public sealed class ReaderChoice
{
    public static readonly int[] Counts = [5, 10, 15];
    public const int DefaultCount = 10, MaxCustom = 20;

    private readonly Dictionary<string, bool> _enabled;
    private readonly List<NewsSource> _custom;

    public int MaxItems { get; private set; }

    public ReaderChoice(int? maxItems = null, IDictionary<string, bool>? enabled = null, IEnumerable<NewsSource>? custom = null)
    {
        MaxItems = maxItems is { } m && Counts.Contains(m) ? m : DefaultCount;
        _enabled = new(enabled ?? new Dictionary<string, bool>());
        _custom = (custom ?? []).Where(c => IsFeedAddress(c.RssUrl)).Take(MaxCustom).Select(c => c with { IsPreset = false }).ToList();
    }

    public IEnumerable<NewsSource> AllSources => NewsSource.Presets.Concat(_custom);
    public IEnumerable<NewsSource> CustomSources => _custom;

    // A source nobody has switched off is on.
    public bool IsOn(NewsSource s) => _enabled.GetValueOrDefault(s.Id, true);
    public IEnumerable<NewsSource> ActiveSources => AllSources.Where(IsOn);

    public void SetCount(int n) { if (Counts.Contains(n)) MaxItems = n; }
    public void Switch(NewsSource s, bool on) => _enabled[s.Id] = on;
    public IReadOnlyDictionary<string, bool> Switches => AllSources.ToDictionary(s => s.Id, IsOn);

    // false = not an http(s) address, no name, or too many.
    public bool AddCustom(string name, string url)
    {
        name = (name ?? "").Trim();
        url = (url ?? "").Trim();
        if (name.Length == 0 || !IsFeedAddress(url) || _custom.Count >= MaxCustom) return false;
        if (name.Length > 60) name = name[..60];
        var source = new NewsSource(Guid.NewGuid().ToString("N")[..12], name, url);
        _custom.Add(source);
        _enabled[source.Id] = true;
        return true;
    }

    public void RemoveCustom(string id) { _custom.RemoveAll(c => c.Id == id); _enabled.Remove(id); }

    public static bool IsFeedAddress(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp);
}

// AI summaries cost money, and the app is free: a hard ceiling per day for the whole app.
public sealed class SummaryBudget(int maxPerDay)
{
    private DateOnly _day;
    private int _used;

    public int MaxPerDay => maxPerDay;

    public bool TryUse(DateTime now)
    {
        var day = DateOnly.FromDateTime(now);
        if (day != _day) (_day, _used) = (day, 0);
        if (_used >= maxPerDay) return false;
        _used++;
        return true;
    }
}

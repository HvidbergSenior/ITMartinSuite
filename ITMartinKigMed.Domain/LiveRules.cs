namespace ITMartinKigMed.Domain;

// Limits that keep a live session pleasant: one message per 3 s per viewer, at most 8 reactions per 5 s -
// enough to be happy, not enough to flood.
public sealed class SendLimits
{
    public static readonly TimeSpan MessageGap = TimeSpan.FromSeconds(3), ReactionWindow = TimeSpan.FromSeconds(5);
    public const int ReactionsPerWindow = 8;

    private readonly Dictionary<string, DateTime> _lastSend = [];
    private readonly Dictionary<string, (DateTime Window, int Count)> _reactRate = [];

    public bool MayAsk(string viewer, DateTime now)
    {
        if (_lastSend.TryGetValue(viewer, out var last) && now - last < MessageGap) return false;
        _lastSend[viewer] = now;
        return true;
    }

    public bool MayReact(string viewer, DateTime now)
    {
        var (window, count) = _reactRate.GetValueOrDefault(viewer);
        if (now - window > ReactionWindow) (window, count) = (now, 0);
        if (count >= ReactionsPerWindow) return false;
        _reactRate[viewer] = (window, count + 1);
        return true;
    }
}

// "Gå live" alone is not proof: a closed or refreshed studio leaves the session marked live.
// No video for 10 minutes while marked live -> the session ends by itself.
public sealed class VideoWatch(DateTime now)
{
    public static readonly TimeSpan AutoEndAfter = TimeSpan.FromMinutes(10);

    private DateTime _lastVideo = now;
    public bool VideoOn { get; private set; }

    public sealed record Verdict(bool VideoChanged, bool EndSession);

    public Verdict Observe(bool videoOn, bool live, DateTime at)
    {
        if (videoOn || !live) _lastVideo = at;   // the 10 minutes count from the moment video should have been there
        var changed = videoOn != VideoOn;
        VideoOn = videoOn;
        return new Verdict(changed, live && !videoOn && at - _lastVideo > AutoEndAfter);
    }
}

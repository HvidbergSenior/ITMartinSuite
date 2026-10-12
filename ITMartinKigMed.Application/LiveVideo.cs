using ITMartinKigMed.Domain;

namespace ITMartinKigMed.Application;

// Asked every 5 s (by a background loop in Infrastructure): is Martin's video arriving?
// - VideoOn feeds /api/status, so itmartin.dk only says "Jeg streamer nu" while there is video.
// - No video for 10 minutes while marked live -> the session ends by itself (archived as usual).
public sealed class LiveVideo(LiveHub hub, IMediaProbe probe, TimeProvider clock)
{
    private readonly VideoWatch _watch = new(clock.GetUtcNow().UtcDateTime);

    public bool VideoOn => _watch.VideoOn;

    // Live for the outside world = marked live AND video arriving.
    public bool IsLive => hub.Read(s => s.Live) && VideoOn;

    public async Task CheckAsync(CancellationToken ct)
    {
        var on = await probe.VideoReadyAsync(ct);
        var verdict = _watch.Observe(on, hub.Read(s => s.Live), clock.GetUtcNow().UtcDateTime);
        if (verdict.EndSession) hub.EndLive();
        if (verdict.VideoChanged) hub.Touch();
    }
}

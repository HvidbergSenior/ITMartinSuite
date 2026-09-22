using System.Collections.Concurrent;

namespace ITMartinUpload.Server.Services;

/// <summary>
/// Keeps a customer's upload burst down to one notification. A phone photo library
/// arrives as hundreds of separate requests within a few minutes; Martin wants to know
/// that they started, not that file 297 of 400 landed.
/// </summary>
public sealed class UploadActivity
{
    private static readonly TimeSpan Quiet = TimeSpan.FromMinutes(30);
    private readonly ConcurrentDictionary<string, DateTime> _lastAnnounced = new(StringComparer.OrdinalIgnoreCase);

    public bool ShouldAnnounceUpload(string slug) => ShouldAnnounceUpload(slug, DateTime.UtcNow);

    /// <summary>Testable overload: true the first time, then not again until the quiet period is over.</summary>
    public bool ShouldAnnounceUpload(string slug, DateTime nowUtc)
    {
        var announce = true;
        _lastAnnounced.AddOrUpdate(slug, nowUtc, (_, previous) =>
        {
            if (nowUtc - previous < Quiet) { announce = false; return previous; }
            return nowUtc;
        });
        return announce;
    }
}

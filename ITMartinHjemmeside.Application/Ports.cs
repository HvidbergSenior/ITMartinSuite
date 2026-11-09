using ITMartinHjemmeside.Domain;

namespace ITMartinHjemmeside.Application;

/// <summary>Where sites live. One site = one folder, so a backup or a hand-over is just that folder.</summary>
public interface ISiteStore
{
    IReadOnlyList<Site> All();
    Site? Get(string id);
    void Save(Site site);
    /// <summary>Keeps a copy of the site as it is now, for Fortryd. Only the newest SiteRules.History copies are kept.</summary>
    void PushHistory(Site site);
    /// <summary>Takes the newest copy off the history, or null when there is nothing to undo.</summary>
    Site? PopHistory(string id);
    int HistoryCount(string id);
}

/// <summary>A site's pictures: shrunk on the way in (phone photo ~5 MB -> ~300 KB), metadata and GPS removed.</summary>
public interface IPictureStore
{
    /// <summary>Returns the stored name, or null when the file is not a picture.</summary>
    Task<string?> SaveAsync(string siteId, Stream content, CancellationToken ct);
    Stream? Open(string siteId, string storedName);
    long UsedBytes(string siteId);
    /// <summary>Removes pictures no page (and no copy in the history) uses any more.</summary>
    void DeleteUnused(string siteId, IReadOnlySet<string> keep);
    /// <summary>The whole site folder as a zip - the hand-over promise: the customer can always take their site with them.</summary>
    Task WriteZipAsync(string siteId, Stream output, CancellationToken ct);
}

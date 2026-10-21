using ITMartinHjem.Domain;

namespace ITMartinHjem.Application;

// The ports of the site itself (texts, pages, timeline, chat, visits). Implemented in ITMartinHjem.Infrastructure;
// every call opens its own short DbContext there, because Blazor Server keeps scoped services for the whole visit.

public interface ISettingsRepository
{
    Task<Settings> GetAsync(CancellationToken ct = default);
    Task SaveAsync(Settings settings, CancellationToken ct = default);
    /// <summary>First start: the one Settings row.</summary>
    Task EnsureAsync(Settings first, CancellationToken ct = default);
}

public interface IDayNoteRepository
{
    Task<DayNote?> GetAsync(string day, CancellationToken ct = default);
    Task<List<DayNote>> AllAsync(CancellationToken ct = default);
    Task<DayNote> SaveAsync(string day, string plan, string done, CancellationToken ct = default);
}

public interface IPageRepository
{
    Task<Page?> BySlugAsync(string slug, CancellationToken ct = default);
    Task<List<Page>> AllAsync(CancellationToken ct = default);
    Task<List<Page>> InMenuAsync(CancellationToken ct = default);
    /// <summary>Sub-pages: slug/…, by Rækkefølge then title.</summary>
    Task<List<Page>> ChildrenAsync(string slug, CancellationToken ct = default);
    Task<bool> SlugTakenAsync(string slug, int exceptId, CancellationToken ct = default);
    /// <returns>The page's id (new pages get one).</returns>
    Task<int> SaveAsync(Page page, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    /// <summary>Does any page body contain this text (e.g. "/f/name.jpg")?</summary>
    Task<bool> AnyUsesAsync(string text, CancellationToken ct = default);
}

public interface IAppLinkRepository
{
    Task<List<AppLink>> AllAsync(CancellationToken ct = default);
    Task<List<AppLink>> ShownAsync(CancellationToken ct = default);
    Task<bool> AnyAsync(CancellationToken ct = default);
    Task SaveAsync(IEnumerable<AppLink> links, CancellationToken ct = default);
    Task<bool> SetAwakeAsync(int id, bool awake, CancellationToken ct = default);
    Task<bool> SetShowAsync(int id, bool show, CancellationToken ct = default);
}

public interface IPostRepository
{
    /// <summary>The timeline, newest first, media in order. Without family access only public posts.</summary>
    Task<List<Post>> TimelineAsync(bool family, CancellationToken ct = default);
    Task<int> CountAsync(Visibility visibility, CancellationToken ct = default);
    Task<Post?> GetAsync(int id, CancellationToken ct = default);
    /// <summary>New post (Id 0) or changes to one; media removed in the editor are taken off.</summary>
    Task SaveAsync(Post edit, IReadOnlyCollection<int> removedMediaIds, CancellationToken ct = default);
    /// <returns>The deleted post with its media (their files must go), or null.</returns>
    Task<Post?> DeleteAsync(int id, CancellationToken ct = default);
    /// <summary>One file with its post (for the visibility check).</summary>
    Task<Media?> MediaAsync(int id, CancellationToken ct = default);
}

public interface IChatRepository
{
    Task<ChatThread?> ByVisitorAsync(string visitorKey, CancellationToken ct = default);
    Task<ChatThread?> ByIdAsync(int id, CancellationToken ct = default);
    Task<List<ChatThread>> NewestAsync(int take, CancellationToken ct = default);
    Task<List<int>> ActiveSinceAsync(DateTime sinceUtc, CancellationToken ct = default);
    /// <summary>Changes one conversation (messages included); saved when <paramref name="change"/> returns true.</summary>
    Task<ChatThread?> ChangeAsync(int id, Func<ChatThread, Task<bool>> change, CancellationToken ct = default);
    /// <summary>Like ChangeAsync, but the visitor's conversation is started when there is none.</summary>
    Task<ChatThread> ChangeForVisitorAsync(string visitorKey, Action<ChatThread> change, CancellationToken ct = default);
    Task<bool> SetVisitorEmailAsync(string visitorKey, string email, CancellationToken ct = default);
    Task<bool> SetVisitorPushAsync(string visitorKey, string push, CancellationToken ct = default);
    Task MarkReadAsync(int id, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public interface IPushSubscriptionRepository
{
    Task<List<PushSub>> AllAsync(CancellationToken ct = default);
    Task AddIfNewAsync(PushSub sub, CancellationToken ct = default);
    Task RemoveAsync(IEnumerable<int> ids, CancellationToken ct = default);
}

public interface IHitRepository
{
    Task<List<string>> OwnerHashesAsync(CancellationToken ct = default);
    Task AddOwnerHashAsync(string hash, CancellationToken ct = default);
    Task PurgeOlderThanAsync(DateTime utc, CancellationToken ct = default);
    /// <returns>The new hit's id.</returns>
    Task<long> AddAsync(Hit hit, CancellationToken ct = default);
    Task SetSecondsAsync(long id, int seconds, DateTime notBeforeUtc, CancellationToken ct = default);
    Task<int> DeleteByVisitorAsync(string visitor, CancellationToken ct = default);
    Task<int> DeleteAllAsync(CancellationToken ct = default);
    Task<List<Hit>> SinceAsync(DateTime utc, CancellationToken ct = default);
    Task<Dictionary<string, DateTime>> LastSeenAsync(CancellationToken ct = default);
}

// ── The outside world ──

public sealed record SavedMedia(Media Media, DateTime? TakenAt);

// Files under <DataDir>/media: pictures upright from EXIF, 2000 px + 800 px thumbnail; everything else as-is.
public interface IMediaStore
{
    Task<SavedMedia> SaveAsync(Stream input, string fileName, string contentType, CancellationToken ct = default);
    void Delete(string storedName, string? thumbName);
    /// <summary>The file on disk, or null when it is not there.</summary>
    string? Find(string storedName);
}

public sealed record PushTarget(string Endpoint, string P256dh, string Auth);
public enum PushResult { Sent, Gone, Failed }

public interface IPushSender
{
    string PublicKey { get; }
    Task<PushResult> SendAsync(PushTarget to, string title, string body, string url, CancellationToken ct = default);
}

public interface IMailSender
{
    /// <returns>True only when the mail really left.</returns>
    Task<bool> SendAsync(string to, string subject, string body, CancellationToken ct = default);
}

public enum TextKind { Liste, Linje, Opslag, Chat }
public sealed record TextReview(string Suggested, List<string> Notes, bool Changed);

// AI proofreading of Martin's own text before it goes on the site - one call per save he presses, capped per day.
public interface ITextChecker
{
    /// <returns>Null when AI is not set up, the cap is reached or it fails: the text is then saved as written.</returns>
    Task<TextReview?> CheckAsync(string text, TextKind kind, CancellationToken ct = default);
}

// A salted hash, so neither IPs nor browser ids are stored as they are.
public interface IVisitorHasher
{
    string Hash(string value);
}

// Is each app on "Mine apps" running (checked in the background)? Null = not checked yet.
public interface IAppStatus
{
    bool? IsUp(string url);
}

// The other apps the front page shows a box from (over the LAN). Null = the app is down or asleep: show nothing.
public sealed record LiveStatus(bool Live, string Title, string Next, int Viewers);
public sealed record ApplianceWindow(DateTime Start, decimal CostKr);
public sealed record ApplianceSnapshot(string Name, decimal KwhPerRun, decimal DurationHours, ApplianceWindow? Now, ApplianceWindow? CheapestToday, ApplianceWindow? CheapestTomorrow);

public interface INeighbourApps
{
    Task<LiveStatus?> KigMedAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ApplianceSnapshot>?> ElPriserAppliancesAsync(CancellationToken ct = default);
}

public sealed class HjemOptions
{
    public string PublicUrl { get; set; } = "https://itmartin.dk";
    public string OwnerEmail { get; set; } = "ITMartin@Mensa.dk";
}

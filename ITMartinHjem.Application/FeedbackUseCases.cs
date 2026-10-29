using ITMartinHjem.Domain;

namespace ITMartinHjem.Application;

public interface IFeedbackStore
{
    Task<List<Feedback>> AllAsync(CancellationToken ct = default);
    Task AddAsync(Feedback f, CancellationToken ct = default);
    /// <summary>Loads, changes and saves in one go; false if it no longer exists.</summary>
    Task<bool> UpdateAsync(int id, Action<Feedback> change, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

// "Kritik": visitors write ros, ris or forslag about how Martin works; Martin reads everything, answers, and shows what
// may be shown. A new one lands in the chat in Svar (IOwnerInbox), so he sees it at once.
public sealed class FeedbackUseCases(IFeedbackStore store, IOwnerInbox inbox, TimeProvider clock)
{
    public async Task<Feedback> WriteAsync(string? kind, string? text, string? name, bool mayShow, CancellationToken ct = default)
    {
        var f = Feedback.Write(kind, text, name, mayShow, clock.GetUtcNow().UtcDateTime);
        await store.AddAsync(f, ct);
        // Kritik that cannot reach Martin is still saved - the chat is only the bell.
        try
        {
            await inbox.NewRequestAsync(f.Name.Length > 0 ? f.Name : "Anonym", "",
                $"Ny {FeedbackRules.Title(f.Kind).ToLowerInvariant()} på /kritik{(f.MayShow ? " (må vises)" : " (kun til dig)")}:\n{f.Text}", ct);
        }
        catch (Exception) { }
        return f;
    }

    public async Task<List<Feedback>> PublicAsync(CancellationToken ct = default) => FeedbackRules.Public(await store.AllAsync(ct));

    /// <summary>Everything, newest first - only for Martin.</summary>
    public async Task<List<Feedback>> AllAsync(CancellationToken ct = default) =>
        (await store.AllAsync(ct)).OrderByDescending(f => f.CreatedAt).ToList();

    /// <summary>What Martin has not answered or shown yet, oldest first - "Venter på dig" on Min side.</summary>
    public async Task<List<Feedback>> WaitingAsync(CancellationToken ct = default) =>
        (await store.AllAsync(ct)).Where(f => f.Waiting).OrderBy(f => f.CreatedAt).ToList();

    public Task<bool> ShowAsync(int id, string? reply, CancellationToken ct = default) => store.UpdateAsync(id, f => f.Show(reply), ct);
    public Task<bool> HideAsync(int id, CancellationToken ct = default) => store.UpdateAsync(id, f => f.Hide(), ct);
    public Task<bool> AnswerAsync(int id, string? reply, CancellationToken ct = default) => store.UpdateAsync(id, f => f.Answer(reply), ct);
    public Task RemoveAsync(int id, CancellationToken ct = default) => store.DeleteAsync(id, ct);
}

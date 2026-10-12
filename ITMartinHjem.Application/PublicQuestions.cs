using ITMartinHjem.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinHjem.Application;

public interface IPublicQuestionStore
{
    Task<List<PublicQuestion>> AllAsync(CancellationToken ct = default);
    Task AddAsync(PublicQuestion q, CancellationToken ct = default);
    /// <summary>Loads, changes and saves in one go; false if it no longer exists.</summary>
    Task<bool> UpdateAsync(int id, Action<PublicQuestion> change, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

// "Det spørger folk om": Martin turns an answered chat into a public, nameless Q&A; visitors search it,
// browse by topic and see the other questions about the same thing.
public sealed class PublicQuestions(IPublicQuestionStore store, TimeProvider clock)
{
    public sealed record Draft(string Question, string Answer, string Topic);
    public sealed record Found(List<PublicQuestion> Questions, List<(string Topic, int Count)> Topics, int Total);

    // Svar: the anonymised starting point - Martin edits it before it goes up.
    public Draft DraftFromChat(IEnumerable<(bool FromOwner, string Text)> messages, string? visitorName)
    {
        var (q, a) = QuestionRules.DraftFromChat(messages, visitorName);
        return new Draft(q, a, "");
    }

    public async Task<PublicQuestion> PublishAsync(Draft d, int? sourceThreadId, CancellationToken ct = default)
    {
        // Anonymised again on the way in, in case a phone number or e-mail was typed while editing.
        var q = PublicQuestion.Publish(QuestionRules.Anonymize(d.Question), QuestionRules.Anonymize(d.Answer), d.Topic,
            clock.GetUtcNow().UtcDateTime, sourceThreadId);
        await store.AddAsync(q, ct);
        return q;
    }

    public async Task UpdateAsync(int id, Draft d, CancellationToken ct = default)
    {
        var question = QuestionRules.Anonymize(d.Question);
        var answer = QuestionRules.Anonymize(d.Answer);
        if (!await store.UpdateAsync(id, q => q.Edit(question, answer, d.Topic), ct))
            throw new InvalidOperationException("Spørgsmålet findes ikke længere.");
    }

    public Task RemoveAsync(int id, CancellationToken ct = default) => store.DeleteAsync(id, ct);

    public async Task<Found> SearchAsync(string? query, string? topic, CancellationToken ct = default)
    {
        var all = await store.AllAsync(ct);
        return new Found(QuestionRules.Search(all, query, topic), QuestionRules.Topics(all), all.Count);
    }

    public async Task<(List<PublicQuestion> Latest, List<(string Topic, int Count)> Topics, int Total)> LatestAsync(int take, CancellationToken ct = default)
    {
        var all = await store.AllAsync(ct);
        return (all.OrderByDescending(q => q.PublishedAt).Take(take).ToList(), QuestionRules.Topics(all), all.Count);
    }

    public async Task<List<PublicQuestion>> RelatedAsync(PublicQuestion q, int max = 5, CancellationToken ct = default) =>
        QuestionRules.Related(q, await store.AllAsync(ct), max);

    // "Flere om det samme" for a whole result list, from one load.
    public async Task<Dictionary<int, List<PublicQuestion>>> RelatedForAsync(IEnumerable<PublicQuestion> shown, int max = 4, CancellationToken ct = default)
    {
        var all = await store.AllAsync(ct);
        return shown.ToDictionary(q => q.Id, q => QuestionRules.Related(q, all, max));
    }

    public async Task<List<string>> TopicNamesAsync(CancellationToken ct = default) =>
        QuestionRules.Topics(await store.AllAsync(ct)).Select(t => t.Topic).ToList();
}

public static class DependencyInjection
{
    /// <summary>The use cases. The store comes from AddHjemInfrastructure.</summary>
    public static IServiceCollection AddHjemApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<PublicQuestions>();
        return services;
    }
}

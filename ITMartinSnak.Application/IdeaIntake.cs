using ITMartinSnak.Domain;

namespace ITMartinSnak.Application;

// Idéer (2026-11-20): what is pasted into a room's Idéer. When only a link was pasted, the page's own title is fetched first,
// so the card reads "Lions Club Aarhus – arrangementer" instead of a long address.
public sealed class IdeaIntake(IPageTitles titles)
{
    public async Task<(Idea? Idea, string? Error)> AddAsync(Group group, string? by, string? pasted, string? fromEndpoint, CancellationToken ct = default)
    {
        string? title = null;
        if (IdeaRules.NeedsTitle(pasted) && IdeaRules.FirstUrl(pasted) is { } url)
            title = await titles.TitleAsync(url, ct);
        return group.AddIdea(by, pasted, title, fromEndpoint);
    }
}

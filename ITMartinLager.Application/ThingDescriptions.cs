using ITMartinLager.Domain;

namespace ITMartinLager.Application;

/// <summary>Tingshoppen/Strikkeshoppen descriptions (2026-11-10): which things wait, and keeping what Vurdering's AI saw.
/// No AI here - Vurdering looks; Lager only decides what to keep (ThingText).</summary>
public sealed class ThingDescriptionUseCases(IItemRepository items, TimeProvider clock)
{
    public const int MaxPerAsk = 8;

    /// <summary>The oldest things waiting for a description, at most take (and never more than MaxPerAsk).</summary>
    public async Task<List<VurderingDescribe>> WaitingAsync(int take, CancellationToken ct = default)
    {
        var all = (await items.OfKindAsync(Kinds.Ting, ct)).Concat(await items.OfKindAsync(Kinds.Strik, ct));
        return all.Where(ThingText.Wants).OrderBy(i => i.Id).Take(Math.Clamp(take, 1, MaxPerAsk))
            .Select(i => new VurderingDescribe(i.Id, i.Kind, i.Title)).ToList();
    }

    /// <summary>Keeps what the AI saw on one thing. False = no such Ting/Strik thing.</summary>
    public async Task<bool> KeepAsync(int id, VurderingDescription d, CancellationToken ct = default)
    {
        if (await items.GetAsync(id, ct) is not { Kind: Kinds.Ting or Kinds.Strik } item) return false;
        ThingText.Apply(item, d.Title, d.Text, d.Low, d.High, clock.GetUtcNow().UtcDateTime);
        await items.UpdateAsync([item], ct);
        return true;
    }
}

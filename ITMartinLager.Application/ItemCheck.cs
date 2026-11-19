using ITMartinLager.Domain;

namespace ITMartinLager.Application;

/// <summary>The AI's suggestion for one thing: what it thinks it is and where that differs from what is saved. Message = why
/// there is no suggestion (no key, no photo, the AI agrees).</summary>
public sealed record ItemSuggestion(Identified? Ai, List<FieldChange> Changes, string? Message);

/// <summary>"AI on Gem" (user 2026-11-19, "1 and 2" + "it is uncertain and i change text or write something. ai need to find the
/// correct one"): after Gem, or after a new photo of the thing, the AI looks at the photo together with what the person typed
/// and suggests the right title, series, number, year. ONE call per press, started by a person - never a loop - on the cheap
/// model and under the daily cap (CLAUDE.md cost rules). Nothing is changed until the person presses Brug forslaget.</summary>
public sealed class ItemCheckUseCases(IItemRepository items, IPhotoStore photos, ICoverReader reader, IVareinfoGateway? vareinfo = null)
{
    public bool Enabled => reader.Enabled;

    public async Task<ItemSuggestion> SuggestAsync(int id, CancellationToken ct = default)
    {
        if (!reader.Enabled) return new(null, [], "AI'en er ikke slået til (Claude-nøglen mangler i Lager).");
        if (await items.GetAsync(id, ct) is not { } item) return new(null, [], "Varen findes ikke længere.");
        var (jpeg, wholePile) = await PhotoAsync(item, ct);
        if (jpeg is null) return new(null, [], "Der er intet billede at kigge på – tag et billede af varen.");
        var ai = await reader.IdentifyAsync(jpeg, wholePile, ItemCorrection.Typed(item), ct);
        if (ai is null) return new(null, [], "AI'en kunne ikke finde tingen på billedet.");
        var changes = ItemCorrection.Changes(item, ai);
        return new(ai, changes, changes.Count == 0 ? "AI'en er enig i det, du har skrevet." : null);
    }

    /// <summary>Brug forslaget: the AI's answer onto the item, saved like a person's correction (new prices are searched).</summary>
    public async Task<Item?> ApplyAsync(int id, Identified ai, ItemUseCases save, CancellationToken ct = default)
    {
        if (await items.GetAsync(id, ct) is not { } item) return null;
        ItemCorrection.Apply(item, ai);
        await save.SaveAsync(item, ct);
        return item;
    }

    // The thing's own photo first; else its box in the pile photo; else the whole pile (then what the person typed tells
    // the AI which one to look at).
    private async Task<(byte[]? Jpeg, bool WholePile)> PhotoAsync(Item item, CancellationToken ct)
    {
        if (item.ItemPhoto != "" && await photos.ReadAsync(item.ItemPhoto, ct) is { } own) return (own, false);
        if (item.PhotoId is not { } pid || vareinfo is null || await vareinfo.PhotoAsync(pid, null, ct) is not { } pile) return (null, false);
        if (item.PileBox is { } box && await photos.CropAsync(pile, box, ct) is { } cut) return (cut, false);
        return (pile, true);
    }
}

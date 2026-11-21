using ITMartinHjem.Domain;
using ITMartinVarer.Contracts;

namespace ITMartinHjem.Application;

public interface ICustomerWishStore
{
    Task<List<CustomerWish>> OfAsync(string customer, CancellationToken ct = default);
    Task<List<string>> AllTextsAsync(CancellationToken ct = default);
    Task AddAsync(CustomerWish wish, CancellationToken ct = default);
    Task RemoveAsync(string customer, int id, CancellationToken ct = default);
    Task<HashSet<int>> SeenAsync(string customer, CancellationToken ct = default);
    Task MarkSeenAsync(string customer, IEnumerable<int> itemIds, CancellationToken ct = default);
    // The mail when a wish turns up (2026-11-21).
    Task<List<string>> CustomersAsync(CancellationToken ct = default);
    Task<HashSet<int>> MailedAsync(string customer, CancellationToken ct = default);
    Task MarkMailedAsync(string customer, IEnumerable<int> itemIds, CancellationToken ct = default);
}

/// <summary>Kunder mails one customer by kundenummer (it holds the e-mail and the afmeld link).</summary>
public interface ICustomerNotifier
{
    Task<bool> NotifyAsync(string code, string subject, string body, CancellationToken ct = default);
}

/// <summary>A product that fits one of the customer's wishes. New = the notice has not been shown for it yet.</summary>
public sealed record WishFound(CustomerWish Wish, ShopItem Item, bool New);

// "Mine ønsker" (user 2026-11-19: "give people with a customer number a way to add Anders And and/or Bones DVD", "when a
// product shows up ... a notification will pop up ... and the customer number will show").
public sealed class WishUseCases(ICustomerWishStore store, IShopGateway shop)
{
    public Task<List<CustomerWish>> MineAsync(string customer, CancellationToken ct = default) => store.OfAsync(customer, ct);

    /// <summary>Everybody's wish texts (no numbers) - they count in "Det leder folk efter".</summary>
    public Task<List<string>> AllTextsAsync(CancellationToken ct = default) => store.AllTextsAsync(ct);

    /// <returns>The error for the person, or null when saved.</returns>
    public async Task<string?> AddAsync(string customer, string? text, CancellationToken ct = default)
    {
        if (customer == "") return "Log ind med dit nummer først.";
        var mine = await store.OfAsync(customer, ct);
        if (WishRules.Check(text, mine.Count) is { } error) return error;
        var clean = WishRules.Clean(text);
        if (mine.Any(w => string.Equals(w.Text, clean, StringComparison.OrdinalIgnoreCase))) return null;
        await store.AddAsync(new CustomerWish { Customer = customer, Text = clean, CreatedAt = DateTime.UtcNow }, ct);
        return null;
    }

    public Task RemoveAsync(string customer, int id, CancellationToken ct = default) => store.RemoveAsync(customer, id, ct);

    /// <summary>What the shop has for each wish right now (sold things are gone from the shop, so they drop out by themselves).
    /// One search per wish, at most 20 wishes - never a loop over the whole shop.</summary>
    public async Task<List<WishFound>> FoundAsync(string customer, CancellationToken ct = default)
    {
        if (customer == "") return [];
        var wishes = await store.OfAsync(customer, ct);
        if (wishes.Count == 0) return [];
        var seen = await store.SeenAsync(customer, ct);
        var found = new List<WishFound>();
        foreach (var w in wishes.Take(WishRules.MaxPerCustomer))
        {
            var (search, kind) = WishRules.Parse(w.Text);
            if (search == "") continue;
            List<ShopItem> items;
            try { items = (await shop.SearchAsync(search, kind, ct)).Items; }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { continue; }
            foreach (var i in items.Where(i => i.Available > 0 && WishRules.Matches(w.Text, i.Title, i.Line, i.Kind)).Take(WishRules.MaxFoundPerWish))
                if (found.All(f => f.Item.Id != i.Id)) found.Add(new WishFound(w, i, !seen.Contains(i.Id)));
        }
        return found;
    }

    public Task MarkSeenAsync(string customer, IEnumerable<int> itemIds, CancellationToken ct = default) =>
        customer == "" ? Task.CompletedTask : store.MarkSeenAsync(customer, itemIds, ct);

    /// <summary>The mail when a wish turns up (user 2026-11-21: "notifications on special wares that customer wants"): for each
    /// customer with wishes, the found things not mailed before -> ONE mail with all of them -> marked mailed. Only what they
    /// asked for (no ads). At most MaxCustomersPerRun per run; Kunder down = tried again next run.</summary>
    public async Task<int> MailFoundAsync(ICustomerNotifier notifier, string shopUrl, CancellationToken ct = default)
    {
        var sent = 0;
        foreach (var customer in (await store.CustomersAsync(ct)).Take(WishRules.MaxCustomersPerRun))
        {
            var mailed = await store.MailedAsync(customer, ct);
            var fresh = (await FoundAsync(customer, ct)).Where(f => !mailed.Contains(f.Item.Id)).ToList();
            if (fresh.Count == 0) continue;
            var (subject, body) = WishRules.Mail(fresh.Select(f => (f.Wish.Text, f.Item.Line, f.Item.Id)).ToList(), shopUrl);
            if (!await notifier.NotifyAsync(customer, subject, body, ct)) continue;
            await store.MarkMailedAsync(customer, fresh.Select(f => f.Item.Id), ct);
            sent++;
        }
        return sent;
    }
}

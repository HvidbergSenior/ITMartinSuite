namespace ITMartinLager.Domain;

/// <summary>Descriptions for things a seller sells through us - Tingshoppen and Strikkeshoppen (user 2026-11-10: "why no
/// description?" + "i want to have ai look on the things"). Vurdering's AI looks at the photo; these rules decide what is kept.</summary>
public static class ThingText
{
    /// <summary>For sale, a Ting or Strik thing with a picture, no text for buyers yet, not looked at before.</summary>
    public static bool Wants(Item i) =>
        i.Kind is Kinds.Strik or Kinds.Ting && i.ItemPhoto != "" && i.Interest.Trim() == "" && i.DescribedAt is null
        && (i.Status == Statuses.Lager || i.Status == Statuses.TilSalg);

    /// <summary>The person's own title wins - unless it is only a word or two ("Stol", "Kabinet") and the AI found a fuller one.</summary>
    public static string Title(string own, string ai)
    {
        own = own.Trim(); ai = (ai ?? "").Trim();
        if (ai.Length == 0) return own;
        var words = own.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        return words <= 2 && ai.Length > own.Length ? (ai.Length > 200 ? ai[..200] : ai) : own;
    }

    /// <summary>Keeps what the AI saw: the title (see Title), the text buyers read (only when there is none - a person's text is
    /// never replaced), and for a Ting without any price the middle of the range as the suggested price. Strik is priced by
    /// the maker, so it gets no suggestion. Always marks it looked at, so an empty answer is not asked again and again.</summary>
    public static void Apply(Item item, string? title, string? text, int? low, int? high, DateTime now)
    {
        item.DescribedAt = now;
        item.Title = Title(item.Title, title ?? "");
        var t = (text ?? "").Trim();
        if (item.Interest.Trim() == "" && t != "") item.Interest = t.Length > 1200 ? t[..1200] : t;
        if (item.Kind == Kinds.Ting && item.Price is null && item.PriceHint is null && Middle(low, high) is { } kr) item.PriceHint = kr;
        item.Touch();
    }

    private static decimal? Middle(int? low, int? high) => (low is > 0 ? low : null, high is > 0 ? high : null) switch
    {
        ({ } l, { } h) => Math.Round((l + h) / 2m / 10m) * 10m,
        ({ } l, null) => l,
        (null, { } h) => h,
        _ => null,
    };
}

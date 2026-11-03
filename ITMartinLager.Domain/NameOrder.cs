using System.Globalization;

namespace ITMartinLager.Domain;

/// <summary>A-Å on bogshoppen.dk (user 2026-11-03: "but it is not alphateciacl?"): the database sorted by Title alone, with capitals
/// before small letters and Å before Æ and Ø - while the card shows "Damien Rice · 9". Now by what the card starts with (the artist
/// or author, else the title), in Danish order (Æ, Ø, Å after Z), small and capital letters alike, then the title, the year and the
/// number as a number (nr. 9 before nr. 10).</summary>
public sealed class NameOrder : IComparer<Item>
{
    public static readonly NameOrder Instance = new();

    private static readonly StringComparer Danish = StringComparer.Create(CultureInfo.GetCultureInfo("da-DK"), CompareOptions.IgnoreCase);

    /// <summary>What the line on the card starts with (Item.Line: Artist first, then Title).</summary>
    public static string Lead(Item i) => (i.Artist.Trim() != "" ? i.Artist : i.Title).Trim();

    /// <summary>The number as printed, as a number when it starts with digits ("12b" = 12); else none (last).</summary>
    public static int NumberOf(Item i)
    {
        var digits = new string(i.Number.Trim().TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, out var n) ? n : int.MaxValue;
    }

    public int Compare(Item? a, Item? b)
    {
        if (ReferenceEquals(a, b)) return 0;
        if (a is null) return -1;
        if (b is null) return 1;
        var c = Danish.Compare(Lead(a), Lead(b));
        if (c == 0) c = Danish.Compare(a.Title.Trim(), b.Title.Trim());
        if (c == 0) c = (a.Year ?? int.MaxValue).CompareTo(b.Year ?? int.MaxValue);
        if (c == 0) c = NumberOf(a).CompareTo(NumberOf(b));
        return c == 0 ? a.Id.CompareTo(b.Id) : c;
    }
}

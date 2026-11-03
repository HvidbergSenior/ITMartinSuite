namespace ITMartinLager.Domain;

/// <summary>A free fingerprint of a cover picture (user 2026-11-04: "what about matching pictures?", chose the free version): the
/// shape (a 64-bit difference hash of the picture made grey and 9x8 small) and the colour layout (the average colour in 4x4
/// squares, R G B per square = 48 bytes). No AI - it runs on the server. It is a hint, not proof: a crooked photo or another
/// background lowers the score.</summary>
public sealed record CoverPrint(ulong Shape, byte[] Layout);

public static class CoverPrints
{
    /// <summary>Listing pictures fetched per book, at most - only for the things a person prices (Skal vurderes).</summary>
    public const int MaxPerItem = 8;

    /// <summary>From this score a listing picture "ligner vores omslag".</summary>
    public const int AlikeFrom = 70;

    /// <summary>How alike two covers look, 0-100: shape and colour layout count half each (the purple 1968 jacket of Ansigterne
    /// is another colour than a later printing). Colour is strict: an average difference of a third of the scale already gives 0.</summary>
    public static int Similarity(CoverPrint a, CoverPrint b)
    {
        var shape = 1 - System.Numerics.BitOperations.PopCount(a.Shape ^ b.Shape) / 64.0;
        var n = Math.Min(a.Layout.Length, b.Layout.Length);
        var diff = n == 0 ? 1 : Enumerable.Range(0, n).Average(i => Math.Abs(a.Layout[i] - b.Layout[i])) / 255.0;
        var colour = Math.Max(0, 1 - diff * 3);
        return (int)Math.Round(100 * (0.5 * shape + 0.5 * colour));
    }

    /// <summary>The difference hash of a 9x8 grey picture (row by row, left to right): bit set when a pixel is brighter than the
    /// next one in its row.</summary>
    public static ulong Shape(byte[,] grey9x8)
    {
        ulong hash = 0;
        var bit = 0;
        for (var y = 0; y < 8; y++)
            for (var x = 0; x < 8; x++, bit++)
                if (grey9x8[x, y] > grey9x8[x + 1, y]) hash |= 1UL << bit;
        return hash;
    }

    /// <summary>The things whose listing pictures are compared: no price a person set, a found price from ShopDeals.AssessFromKr,
    /// and our own picture of it.</summary>
    public static bool Worth(Item i) =>
        i.Price is not > 0 && i.PriceHint >= ITMartinVarer.Contracts.ShopDeals.AssessFromKr && i.ItemPhoto != "";
}

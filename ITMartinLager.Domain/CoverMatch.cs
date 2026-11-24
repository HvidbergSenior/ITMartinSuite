namespace ITMartinLager.Domain;

// Same cover or another edition (user 2026-11-24, Stikprøve: "Goodbye Mr. Chips -> look at the pictures from front cover",
// "Make This Moment - look at where the text is"). The text of a listing can be exactly ours while the edition is another, and only
// the cover shows it. OpenCV feature matching (ORB + RANSAC) counts points that sit in the same place on both covers. Tested on
// 207 marked listings: from 20 points it kept 88 of 90 Rigtig and removed 60 of 71 Forkert, when our own photo is a front cover.
public static class CoverMatch
{
    /// <summary>From this many matching points the listing shows the same cover.</summary>
    public const int SameFrom = 20;

    /// <summary>Our own photo is a front cover (not a thin spine cut from a pile) - only then the comparison means anything.</summary>
    public static bool CoverShaped(int? w, int? h) => w is > 0 && h is > 0 && (double)w / h is >= 0.45 and <= 2.2;

    /// <summary>The listing picture was compared with our cover and shows another one (-1 = the picture could not be fetched: unknown).</summary>
    public static bool OtherEdition(PriceFind f) => f.Cover is { } points && points >= 0 && points < SameFrom;
}

using ITMartinPolstrer.Core.Data.Entities;

namespace ITMartinPolstrer.Core.Services;

// Danish labels shared by the phone app and the workshop board.
public static class PieceText
{
    public static string StatusText(PieceStatus s) => s switch
    {
        PieceStatus.Modtaget => "Modtaget",
        PieceStatus.IGang => "I gang",
        PieceStatus.Faerdig => "Færdig",
        PieceStatus.Afleveret => "Afleveret",
        _ => s.ToString()
    };

    public static string DeadlineText(DateOnly d)
    {
        var days = d.DayNumber - DateOnly.FromDateTime(DateTime.Now).DayNumber;
        if (days < 0) return $"Overskredet {-days} dg";
        if (days == 0) return "I dag";
        if (days == 1) return "I morgen";
        return $"{days} dage – {d:d. MMM}";
    }

    public static string UrgencyClass(Piece p)
    {
        if (p.Status >= PieceStatus.Faerdig || p.Deadline is null) return "";
        var days = p.Deadline.Value.DayNumber - DateOnly.FromDateTime(DateTime.Now).DayNumber;
        return days < 0 ? "urgent-over" : days <= 3 ? "urgent-soon" : "";
    }

    public static string TitleOrDefault(Piece p) => string.IsNullOrWhiteSpace(p.Title) ? "Uden navn" : p.Title;

    public static MediaFile? Cover(Piece p) =>
        p.Steps.SelectMany(s => s.Media).Where(m => !m.IsVideo).OrderBy(m => m.UploadedAt).FirstOrDefault();
}

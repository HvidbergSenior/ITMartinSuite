namespace ITMartin.Magic.Application.Models;

public static class OcrGeometryProfiles
{
    public static readonly OcrGeometryProfile Modern =
        new()
        {
            TitleX = 0.085,
            TitleY = 0.032,
            TitleWidth = 0.50,
            TitleHeight = 0.026,

            BottomX = 0.055,
            BottomY = 0.948,
            BottomWidth = 0.36,
            BottomHeight = 0.018,

            ArtistX = 0.43,
            ArtistY = 0.948,
            ArtistWidth = 0.22,
            ArtistHeight = 0.018,

            SetX = 0.77,
            SetY = 0.60,
            SetWidth = 0.10,
            SetHeight = 0.07
        };

    public static readonly OcrGeometryProfile OldBorder =
        new()
        {
            TitleX = 0.040,
            TitleY = 0.025,
            TitleWidth = 0.42,
            TitleHeight = 0.045,

            BottomX = 0.030,
            BottomY = 0.935,
            BottomWidth = 0.40,
            BottomHeight = 0.030,

            SetX = 0,
            SetY = 0,
            SetWidth = 0,
            SetHeight = 0
        };
}
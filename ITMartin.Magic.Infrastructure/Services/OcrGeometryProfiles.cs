using ITMartin.Magic.Application.Models;
using ITMartin.Magic.Domain;
using ITMartin.Magic.Infrastructure.Enums;

namespace ITMartin.Magic.Infrastructure.Services;

public static class OcrGeometryProfiles
{
    public static OcrGeometryProfile Get(
        CardLayoutType layout)
    {
        switch (layout)
        {
            // =====================================
            // OLD BORDER
            // =====================================

            case CardLayoutType.OldBorder:

                return new OcrGeometryProfile
                {
                    TitleX = 0.045,
                    TitleY = 0.020,
                    TitleWidth = 0.62,
                    TitleHeight = 0.050,

                    BottomX = 0.040,
                    BottomY = 0.955,
                    BottomWidth = 0.34,
                    BottomHeight = 0.020,

                    ArtistX = 0.36,
                    ArtistY = 0.955,
                    ArtistWidth = 0.34,
                    ArtistHeight = 0.020,

                    SetX = 0.77,
                    SetY = 0.60,
                    SetWidth = 0.10,
                    SetHeight = 0.07
                };

            // =====================================
            // MODERN
            // =====================================

            default:

                return new OcrGeometryProfile
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
        }
    }
}
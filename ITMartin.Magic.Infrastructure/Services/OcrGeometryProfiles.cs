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
                    // =====================================
                    // TITLE
                    // =====================================

                    TitleX = 0.030,
                    TitleY = 0.018,
                    TitleWidth = 0.68,
                    TitleHeight = 0.045,

                    // =====================================
// COPYRIGHT / BOTTOM
// =====================================

                    BottomX = 0.030,
                    BottomY = 0.930,
                    BottomWidth = 0.52,
                    BottomHeight = 0.045,

// =====================================
// ARTIST
// =====================================

                    ArtistX = 0.54,
                    ArtistY = 0.930,
                    ArtistWidth = 0.28,
                    ArtistHeight = 0.045,


                    // =====================================
                    // UNUSED FOR OLD BORDER
                    // =====================================

                    SetX = 0,
                    SetY = 0,
                    SetWidth = 0,
                    SetHeight = 0
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
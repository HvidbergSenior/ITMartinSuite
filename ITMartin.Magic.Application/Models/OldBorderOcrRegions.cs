using OpenCvSharp;

namespace ITMartin.Magic.Application.Models;

public static class OldBorderOcrRegions
{
    public static readonly Rect Title =
        new(
            20,
            0,
            2200,
            400);

    public static readonly Rect SetArea =
        new(
            1450,
            1600,
            850,
            850);

    public static readonly Rect BottomInfo =
        new(
            0,
            2700,
            2400,
            660);
}
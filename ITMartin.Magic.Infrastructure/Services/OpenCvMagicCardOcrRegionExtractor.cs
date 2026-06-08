using ITMartin.Magic.Application.Models;
using ITMartin.OCR.Interfaces;
using ITMartin.OCR.Models;
using OpenCvSharp;

namespace ITMartin.Magic.Infrastructure.Services;

public sealed class OpenCvMagicCardOcrRegionExtractor
    : IOcrRegionExtractor
{
    public Task<OcrRegionResult?> ExtractAsync(
        string normalizedCardPath,
        CancellationToken cancellationToken)
    {
        var result =
            Extract(
                normalizedCardPath,
                cancellationToken);

        return Task.FromResult(result);
    }

    private OcrRegionResult? Extract(
        string normalizedCardPath,
        CancellationToken cancellationToken)
    {
        using var image =
            Cv2.ImRead(
                normalizedCardPath,
                ImreadModes.Color);

        if (image.Empty())
        {
            return null;
        }

        var width =
            image.Width;

        var height =
            image.Height;

        var profile =
            OcrGeometryProfiles.Modern;

        var titleRect =
            CreateRect(
                width,
                height,
                profile.TitleX,
                profile.TitleY,
                profile.TitleWidth,
                profile.TitleHeight);

        var bottomRect =
            CreateRect(
                width,
                height,
                profile.BottomX,
                profile.BottomY,
                profile.BottomWidth,
                profile.BottomHeight);

        var setRect =
            CreateRect(
                width,
                height,
                profile.SetX,
                profile.SetY,
                profile.SetWidth,
                profile.SetHeight);

        var folder =
            Path.Combine(
                Directory.GetCurrentDirectory(),
                "data",
                "ocr");

        Directory.CreateDirectory(
            folder);

        Console.WriteLine(
            $"Image: {width}x{height}");

        Console.WriteLine(
            $"TITLE RECT: {titleRect}");

        Console.WriteLine(
            $"BOTTOM RECT: {bottomRect}");

        Console.WriteLine(
            $"SET RECT: {setRect}");

        using var debug =
            image.Clone();

        Cv2.Rectangle(
            debug,
            titleRect,
            Scalar.Red,
            6);

        Cv2.Rectangle(
            debug,
            bottomRect,
            Scalar.Blue,
            6);

        Cv2.Rectangle(
            debug,
            setRect,
            Scalar.Yellow,
            6);

        Cv2.ImWrite(
            Path.Combine(
                folder,
                "ocr_regions.jpg"),
            debug);

        return new OcrRegionResult
        {
            TitleImagePath =
                SaveCrop(
                    image,
                    titleRect,
                    folder,
                    "title"),

            BottomInfoImagePath =
                SaveCrop(
                    image,
                    bottomRect,
                    folder,
                    "bottom"),

            SetCodeImagePath =
                SaveCrop(
                    image,
                    setRect,
                    folder,
                    "set"),

            FullCardImagePath =
                normalizedCardPath
        };
    }

    private static Rect CreateRect(
        int width,
        int height,
        double x,
        double y,
        double w,
        double h)
    {
        return new Rect(
            (int)(width * x),
            (int)(height * y),
            (int)(width * w),
            (int)(height * h));
    }

    private static string SaveCrop(
        Mat source,
        Rect rect,
        string folder,
        string name)
    {
        using var crop =
            new Mat(source, rect);

        using var gray =
            new Mat();

        Cv2.CvtColor(
            crop,
            gray,
            ColorConversionCodes.BGR2GRAY);

        var output =
            Path.Combine(
                folder,
                $"{name}_{Guid.NewGuid()}.png");

        Cv2.ImWrite(
            output,
            gray);

        return output;
    }
}
using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Application.Models;
using ITMartin.OCR.Interfaces;
using ITMartin.OCR.Models;
using OpenCvSharp;

namespace ITMartin.Magic.Infrastructure.Services;

public sealed class OpenCvMagicCardOcrRegionExtractor
    : IOcrRegionExtractor
{
    private readonly
        ICardLayoutDetectionService
        _layoutDetectionService;

    public OpenCvMagicCardOcrRegionExtractor(
        ICardLayoutDetectionService layoutDetectionService)
    {
        _layoutDetectionService =
            layoutDetectionService;
    }

    public Task<OcrRegionResult?> ExtractAsync(
        string normalizedCardPath)
    {
        var result =
            Extract(normalizedCardPath);

        return Task.FromResult(result);
    }

    private OcrRegionResult? Extract(
        string normalizedCardPath)
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

        var layoutType =
            _layoutDetectionService
                .Detect(normalizedCardPath);

        var profile =
            OcrGeometryProfiles
                .Get(layoutType);

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

        var artistRect =
            CreateRect(
                width,
                height,
                profile.ArtistX,
                profile.ArtistY,
                profile.ArtistWidth,
                profile.ArtistHeight);

        Rect setRect;

        if (layoutType ==
            CardLayoutType.OldBorder)
        {
            setRect =
                new Rect(0, 0, 1, 1);
        }
        else
        {
            setRect =
                CreateRect(
                    width,
                    height,
                    profile.SetX,
                    profile.SetY,
                    profile.SetWidth,
                    profile.SetHeight);
        }

        var folder =
            Path.Combine(
                Directory.GetCurrentDirectory(),
                "data",
                "ocr");

        Directory.CreateDirectory(
            folder);

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

            ArtistImagePath =
                SaveCrop(
                    image,
                    artistRect,
                    folder,
                    "artist"),

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

        using var resized =
            new Mat();

        Cv2.Resize(
            crop,
            resized,
            new OpenCvSharp.Size(
                crop.Width * 4,
                crop.Height * 4),
            0,
            0,
            InterpolationFlags.Cubic);

        using var gray =
            new Mat();

        Cv2.CvtColor(
            resized,
            gray,
            ColorConversionCodes.BGR2GRAY);

        using var denoised =
            new Mat();

        Cv2.FastNlMeansDenoising(
            gray,
            denoised,
            10);

        using var contrasted =
            new Mat();

        denoised.ConvertTo(
            contrasted,
            -1,
            1.4,
            10);

        using var processed =
            new Mat();

        var kernel =
            InputArray.Create(
                new float[,]
                {
                    { 0, -1, 0 },
                    { -1, 5, -1 },
                    { 0, -1, 0 }
                });

        Cv2.Filter2D(
            contrasted,
            processed,
            -1,
            kernel);

        if (name == "bottom" || name == "artist")
        {
            Cv2.Threshold(
                processed,
                processed,
                140,
                255,
                ThresholdTypes.Binary);
        }

        var output =
            Path.Combine(
                folder,
                $"{name}_{Guid.NewGuid()}.png");

        Cv2.ImWrite(
            output,
            processed);

        return output;
    }
}
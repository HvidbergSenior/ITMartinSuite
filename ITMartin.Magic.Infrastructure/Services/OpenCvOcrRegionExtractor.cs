using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Application.Models;
using ITMartin.Magic.Domain;
using OpenCvSharp;

namespace ITMartin.Magic.Infrastructure.Services;

public class OpenCvOcrRegionExtractor
    : IOcrRegionExtractor
{
    private readonly
        ICardLayoutDetectionService
        _layoutDetectionService;

    public OpenCvOcrRegionExtractor(
        ICardLayoutDetectionService layoutDetectionService)
    {
        _layoutDetectionService =
            layoutDetectionService;
    }
    public async Task<OcrRegionResult?> ExtractAsync(
        string normalizedCardPath)
    {
        return await Task.Run(() =>
        {
            using var image =
                Cv2.ImRead(normalizedCardPath);

            if (image.Empty())
            {
                return null;
            }

            var width =
                image.Width;

            var height =
                image.Height;

            Console.WriteLine(
                $"NORMALIZED SIZE: {width}x{height}");

            // =====================================
// LAYOUT DETECTION
// =====================================

            var layout =
                _layoutDetectionService
                    .Detect(normalizedCardPath);

            Console.WriteLine(
                $"DETECTED LAYOUT: {layout}");

// =====================================
// OCR PROFILE
// =====================================

            var profile =
                OcrGeometryProfiles
                    .Get(layout);

// =====================================
// OCR GEOMETRY
// =====================================

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

// =====================================
// OLD BORDER:
// NO SET SYMBOL
// =====================================

            Rect setRect;

            if (layout == CardLayoutType.OldBorder)
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
            // =====================================
            // DEBUG OVERLAY
            // =====================================

            using var debug =
                image.Clone();

            Cv2.Rectangle(
                debug,
                titleRect,
                Scalar.Red,
                4);

            Cv2.Rectangle(
                debug,
                bottomRect,
                Scalar.Green,
                4);

            Cv2.Rectangle(
                debug,
                artistRect,
                Scalar.Magenta,
                4);

            Cv2.Rectangle(
                debug,
                setRect,
                Scalar.Yellow,
                4);

            var folder =
                Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "data",
                    "ocr");

            Directory.CreateDirectory(
                folder);

            var debugPath =
                Path.Combine(
                    folder,
                    $"debug_{Guid.NewGuid()}.jpg");

            Cv2.ImWrite(
                debugPath,
                debug);

            Console.WriteLine(
                $"OCR DEBUG: {debugPath}");

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
        });
    }

    // =========================================
    // CREATE RECT
    // =========================================

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

    // =========================================
    // SAVE CROP
    // =========================================

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

// =====================================
// UPSCALE
// =====================================

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

// =====================================
// LIGHT DENOISE
// =====================================

        using var denoised =
            new Mat();

        Cv2.FastNlMeansDenoising(
            gray,
            denoised,
            10);

// =====================================
// LIGHT CONTRAST
// =====================================

        using var contrasted =
            new Mat();

        denoised.ConvertTo(
            contrasted,
            -1,
            1.4,
            10);

// =====================================
// LIGHT SHARPEN
// =====================================

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

// =====================================
// SAVE
// =====================================

        var output =
            Path.Combine(
                folder,
                $"{name}_{Guid.NewGuid()}.png");

        Cv2.ImWrite(
            output,
            processed);

        Console.WriteLine(
            $"OCR CROP [{name}]: {output}");

        return output;

    }

}
using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Application.Models;
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
            // MTG OCR GEOMETRY
            // =====================================

            var titleRect =
                CreateRect(
                    width,
                    height,
                    0.085,
                    0.032,
                    0.50,
                    0.026);

            var bottomRect =
                CreateRect(
                    width,
                    height,
                    0.055,
                    0.948,
                    0.36,
                    0.018);

            var artistRect =
                CreateRect(
                    width,
                    height,
                    0.43,
                    0.948,
                    0.22,
                    0.018);

            var setRect =
                CreateRect(
                    width,
                    height,
                    0.77,
                    0.60,
                    0.10,
                    0.07);

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

        Cv2.Resize(
            crop,
            resized,
            new OpenCvSharp.Size(
                crop.Width * 8,
                crop.Height * 8),
            0,
            0,
            InterpolationFlags.Lanczos4);

        using var processed =
            new Mat();

        // =====================================
        // TITLE OCR
        // =====================================

        if (name == "title")
        {
            using var hsv =
                new Mat();

            Cv2.CvtColor(
                resized,
                hsv,
                ColorConversionCodes.BGR2HSV);

            // =================================
            // EXTRACT BRIGHT TITLE TEXT
            // =================================

            using var mask =
                new Mat();

            Cv2.InRange(
                hsv,
                new Scalar(0, 0, 140),
                new Scalar(180, 80, 255),
                mask);

            // =================================
            // CLEANUP
            // =================================

            using var kernel =
                Cv2.GetStructuringElement(
                    MorphShapes.Rect,
                    new OpenCvSharp.Size(3, 3));

            Cv2.MorphologyEx(
                mask,
                mask,
                MorphTypes.Close,
                kernel);

            Cv2.GaussianBlur(
                mask,
                processed,
                new OpenCvSharp.Size(3, 3),
                0);
        }

        // =====================================
        // NORMAL OCR
        // =====================================

        else
        {
            using var gray =
                new Mat();

            Cv2.CvtColor(
                resized,
                gray,
                ColorConversionCodes.BGR2GRAY);

            Cv2.AdaptiveThreshold(
                gray,
                processed,
                255,
                AdaptiveThresholdTypes.GaussianC,
                ThresholdTypes.Binary,
                31,
                8);
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
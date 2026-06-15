using ITMartin.Magic.Application.Models;
using ITMartin.OCR.Interfaces;
using ITMartin.OCR.Models;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace ITMartin.Magic.Infrastructure.Services;

public sealed class OpenCvMagicCardOcrRegionExtractor
    : IOcrRegionExtractor
{
    private readonly ILogger<OpenCvMagicCardOcrRegionExtractor> _logger;

    public OpenCvMagicCardOcrRegionExtractor(ILogger<OpenCvMagicCardOcrRegionExtractor> logger)
    {
        _logger = logger;
    }

    public Task<OcrRegionResult?> ExtractAsync(
        string normalizedCardPath,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("OCR source file: {Path}", normalizedCardPath);
        
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

        var folder =
            Path.Combine(
                Directory.GetCurrentDirectory(),
                "data",
                "ocr");

        Directory.CreateDirectory(
            folder);

        Cv2.ImWrite(
            Path.Combine(
                folder,
                "ocr_source.jpg"),
            image);
        if (image.Empty())
        {
            return null;
        }

        var width =
            image.Width;

        var height =
            image.Height;
        _logger.LogDebug("Image dimensions: {Width}x{Height}", width, height);
        var profile =
            OcrGeometryProfiles.All;

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

        folder =
            Path.Combine(
                Directory.GetCurrentDirectory(),
                "data",
                "ocr");

        Directory.CreateDirectory(
            folder);

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
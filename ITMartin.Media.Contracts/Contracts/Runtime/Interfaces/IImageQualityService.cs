namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

public interface IImageQualityService
{
    /// <summary>
    /// Free, local blur/solid-color check on a downscaled grayscale copy of
    /// the image - the same free-tier-before-paid-tier pattern as the ONNX
    /// rotation check, so most photos never need the paid Claude vision call
    /// to answer QualityChecked. Returns (false, false) if the file can't be
    /// decoded, rather than treating a decode failure as "confirmed bad".
    /// </summary>
    Task<(bool IsBlurry, bool IsSolidColor)> AnalyzeAsync(string imagePath, CancellationToken cancellationToken = default);
}

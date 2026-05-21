namespace ITMartin.OCR.Models;

public sealed class OcrResult
{
    public IReadOnlyCollection<
            OcrTextRegionResult>
        Regions { get; init; }
        =
        [];
    public string FullText =>
        string.Join(
            Environment.NewLine,
            Regions.Select(x => x.Text));
}
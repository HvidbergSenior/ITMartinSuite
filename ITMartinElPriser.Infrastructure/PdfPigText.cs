using ITMartinElPriser.Application;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace ITMartinElPriser.Infrastructure;

// The bill's text with PdfPig, in reading order. Read in memory only - nothing is saved.
public sealed class PdfPigText : IPdfText
{
    public string Read(byte[] pdf)
    {
        using var doc = PdfDocument.Open(pdf);
        return string.Join("\n", doc.GetPages().Select(p => ContentOrderTextExtractor.GetText(p)));
    }
}

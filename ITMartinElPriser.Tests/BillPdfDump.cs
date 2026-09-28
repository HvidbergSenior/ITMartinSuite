using NUnit.Framework;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace ITMartinElPriser.Tests;

// Local only: dumps the text PdfPig gets from a real bill. The bill itself (name, address) never
// goes into the repo - point ELBILL_PDF at a file on this machine.
[TestFixture, Explicit("needs a real bill on this machine")]
public class BillPdfDump
{
    [Test]
    public void Dump()
    {
        var path = Environment.GetEnvironmentVariable("ELBILL_PDF");
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) Assert.Ignore("ELBILL_PDF not set");
        using var pdf = PdfDocument.Open(path);
        var text = string.Join("\n\f\n", pdf.GetPages().Select(p => ContentOrderTextExtractor.GetText(p)));
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "elbill.txt"), text);
    }
}

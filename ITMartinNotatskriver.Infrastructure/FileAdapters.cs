using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using ITMartinNotatskriver.Application;
using ITMartinNotatskriver.Domain;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace ITMartinNotatskriver.Infrastructure;

/// <summary>Reads uploads. Photos are turned upright, shrunk to 1800 px (cheaper, and enough to read handwriting)
/// and lose their EXIF data (no GPS or camera details leave the visitor).</summary>
public sealed class AttachmentReader : IAttachmentReader
{
    public const int MaxImageSide = 1800;

    public async Task<Attachment> ReadAsync(string fileName, string contentType, Stream content, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        switch (AttachmentKinds.Detect(fileName, contentType))
        {
            case AttachmentKind.Image:
                try
                {
                    using var img = Image.Load(bytes);
                    img.Mutate(x => x.AutoOrient().Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new Size(MaxImageSide, MaxImageSide) }));
                    img.Metadata.ExifProfile = null;
                    using var jpeg = new MemoryStream();
                    await img.SaveAsJpegAsync(jpeg, new JpegEncoder { Quality = 85 }, ct);
                    return Attachment.Image(fileName, jpeg.ToArray());
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    throw new NotatskriverException($"Billedet {fileName} kunne ikke læses. På iPhone: vælg 'Mest kompatibel' under Indstillinger → Kamera → Formater.");
                }
            case AttachmentKind.Pdf:
                return Attachment.Pdf(fileName, bytes);
            case AttachmentKind.Text when Path.GetExtension(fileName).Equals(".docx", StringComparison.OrdinalIgnoreCase):
                try
                {
                    using var doc = WordprocessingDocument.Open(new MemoryStream(bytes), false);
                    var paragraphs = doc.MainDocumentPart?.Document.Body?.Descendants<W.Paragraph>().Select(p => p.InnerText) ?? [];
                    return Attachment.FromText(fileName, string.Join("\n", paragraphs));
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    throw new NotatskriverException($"Word-filen {fileName} kunne ikke læses.");
                }
            case AttachmentKind.Text:
                return Attachment.FromText(fileName, Encoding.UTF8.GetString(bytes));
            default:
                throw new NotatskriverException($"{fileName}: den filtype kan ikke læses. Brug billede, PDF, Word (.docx) eller tekst.");
        }
    }
}

/// <summary>The finished text as a simple Word document: one paragraph per line, headings in bold.</summary>
public sealed class WordExporter : IWordExporter
{
    public byte[] Export(string text)
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            var body = new W.Body();
            var lines = text.Replace("\r", "").Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var run = new W.Run(new W.Text(lines[i]) { Space = SpaceProcessingModeValues.Preserve });
                if (WrittenDocument.IsHeading(lines, i)) run.RunProperties = new W.RunProperties(new W.Bold());
                body.Append(new W.Paragraph(run));
            }
            doc.AddMainDocumentPart().Document = new W.Document(body);
        }
        return ms.ToArray();
    }
}

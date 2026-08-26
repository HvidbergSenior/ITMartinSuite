using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using ITMartin.Documents.Interfaces;
using ITMartin.Documents.Models;

namespace ITMartin.Documents.Services;

// Parses an uploaded .docx into plain sections so a team never needs Word to
// read or edit it - the original file stays untouched wherever the caller
// stores it; this only produces the editable working copy. Uses the official
// Open XML SDK rather than ad-hoc unzip+regex, since a real product feature
// (not a one-off debug script) needs to handle actual paragraph/heading
// structure correctly.
public sealed class DocxImportService : IDocxImportService
{
    public List<ParsedDocumentSection> ParseDocx(Stream docxStream)
    {
        var sections = new List<ParsedDocumentSection>();
        using var wordDoc = WordprocessingDocument.Open(docxStream, false);
        var body = wordDoc.MainDocumentPart?.Document.Body;
        if (body is null) return sections;

        var order = 0;
        string? pendingHeading = null;

        foreach (var paragraph in body.Elements<Paragraph>())
        {
            var text = string.Concat(paragraph.Descendants<Text>().Select(t => t.Text)).Trim();
            if (text.Length == 0) continue;

            var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
            var isHeading = styleId is not null && styleId.Contains("Heading", StringComparison.OrdinalIgnoreCase);

            if (isHeading)
            {
                // A heading with no body text yet becomes the heading for
                // whichever paragraph(s) follow it, rather than its own
                // empty section.
                pendingHeading = text;
                continue;
            }

            sections.Add(new ParsedDocumentSection
            {
                SortOrder = order++,
                Heading = pendingHeading,
                Text = text,
            });
            pendingHeading = null;
        }

        return sections;
    }
}

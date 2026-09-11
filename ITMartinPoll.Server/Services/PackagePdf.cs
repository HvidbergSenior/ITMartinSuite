using ITMartinPoll.Server.Data;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ITMartinPoll.Server.Services;

// The paper version of a package: the same questions as the web page, with
// boxes to tick and lines to write on, for the neighbours who will not use a
// link. Built from the live package so the two can never drift apart -
// whatever is on the page is on the paper.
public static class PackagePdf
{
    private static readonly string Accent = "#4338CA";
    private static readonly string Muted = "#5b6172";
    private static readonly string Line = "#c9cdd8";

    public static byte[] Render(Package package)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var steps = (package.Instructions ?? "")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            // The last web step ("gemmes automatisk") makes no sense on paper.
            .Where(s => !s.Contains("gemmes", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(18, Unit.Millimetre);
                page.DefaultTextStyle(t => t.FontSize(11).FontFamily("Arial"));

                page.Header().Column(col =>
                {
                    if (!string.IsNullOrWhiteSpace(package.Audience))
                        col.Item().Text(package.Audience.ToUpperInvariant()).FontSize(10).Bold().FontColor(Accent).LetterSpacing(0.08f);
                    col.Item().Text(package.Title).FontSize(20).Bold();
                    col.Item().PaddingBottom(6);
                });

                page.Content().Column(col =>
                {
                    col.Spacing(10);

                    if (!string.IsNullOrWhiteSpace(package.Intro))
                        col.Item().Text(package.Intro).FontColor(Muted).LineHeight(1.4f);

                    if (steps.Count > 0)
                    {
                        col.Item().PaddingTop(4).Text("Sådan gør du").FontSize(10).Bold().FontColor(Accent).LetterSpacing(0.06f);
                        foreach (var (step, i) in steps.Select((s, i) => (s, i + 1)))
                            col.Item().Row(r =>
                            {
                                r.ConstantItem(16).Text($"{i}.").FontColor(Muted);
                                r.RelativeItem().Text(step);
                            });
                    }

                    // Who is answering - same two fields as the page.
                    col.Item().PaddingTop(6).Row(r =>
                    {
                        r.RelativeItem().Column(c => { c.Item().Text("Fornavn").FontSize(9).FontColor(Muted); c.Item().PaddingTop(10).LineHorizontal(0.8f).LineColor(Line); });
                        r.ConstantItem(16);
                        r.RelativeItem().Column(c => { c.Item().Text("Husstandsnummer").FontSize(9).FontColor(Muted); c.Item().PaddingTop(10).LineHorizontal(0.8f).LineColor(Line); });
                    });

                    foreach (var poll in package.Polls.Where(p => p.IsActive).OrderBy(p => p.Id))
                    {
                        col.Item().PaddingTop(10).Column(q =>
                        {
                            q.Item().Text(poll.Title).FontSize(13).Bold();
                            if (!string.IsNullOrWhiteSpace(poll.Body))
                                q.Item().Text(poll.Body).FontSize(10).FontColor(Muted);

                            q.Item().PaddingTop(4).Column(opts =>
                            {
                                foreach (var o in poll.Options.OrderBy(o => o.SortOrder))
                                    opts.Item().PaddingTop(3).Row(r =>
                                    {
                                        r.ConstantItem(14).Height(11).Border(0.8f).BorderColor("#333333");
                                        r.ConstantItem(8);
                                        r.RelativeItem().Text(o.Label);
                                    });
                            });

                            CommentLines(q, "Vil du uddybe?", 2);
                        });
                    }

                    foreach (var session in package.Sessions.Where(s => s.IsActive).OrderBy(s => s.Id))
                    {
                        col.Item().PaddingTop(12).Column(s =>
                        {
                            s.Item().Text(session.Title).FontSize(13).Bold();
                            if (!string.IsNullOrWhiteSpace(session.Description))
                                s.Item().Text(session.Description).FontSize(10).FontColor(Muted);
                            s.Item().Text("Valgfrit — bedøm kun dem du kender. 1 = " + RatingScale.Label(1) + ", 5 = " + RatingScale.Label(5) + ".").FontSize(9).FontColor(Muted);

                            s.Item().PaddingTop(6).Table(t =>
                            {
                                t.ColumnsDefinition(c =>
                                {
                                    c.RelativeColumn(3);
                                    for (var i = 1; i <= 5; i++) c.ConstantColumn(26);
                                });
                                t.Header(h =>
                                {
                                    h.Cell().Text("");
                                    for (var i = 1; i <= 5; i++)
                                        h.Cell().AlignCenter().Text(i.ToString()).FontSize(9).FontColor(Muted);
                                });
                                foreach (var img in session.Images.OrderBy(i => i.SortOrder))
                                {
                                    t.Cell().PaddingVertical(3).Text(img.Caption);
                                    for (var i = 1; i <= 5; i++)
                                        t.Cell().PaddingVertical(3).AlignCenter().Element(e => e.Width(12).Height(11).Border(0.8f).BorderColor("#333333"));
                                }
                            });

                            CommentLines(s, "Kommentar til ladeoperatørerne", 3);
                        });
                    }
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Aflever det udfyldte skema — eller svar på nettet: stem.itmartin.dk").FontSize(9).FontColor(Muted);
                });
            });
        }).GeneratePdf();
    }

    private static void CommentLines(ColumnDescriptor col, string label, int lines)
    {
        col.Item().PaddingTop(6).Text(label + " (valgfrit)").FontSize(9).FontColor(Muted);
        for (var i = 0; i < lines; i++)
            col.Item().PaddingTop(14).LineHorizontal(0.6f).LineColor(Line);
    }
}

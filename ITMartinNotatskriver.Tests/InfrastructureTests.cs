using DocumentFormat.OpenXml.Packaging;
using FluentAssertions;
using ITMartinNotatskriver.Application;
using ITMartinNotatskriver.Domain;
using ITMartinNotatskriver.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace ITMartinNotatskriver.Tests;

/// <summary>The real adapters against real files (no network).</summary>
public class AttachmentReaderTests
{
    [Test]
    public async Task Photos_are_shrunk_and_lose_their_GPS_data()
    {
        using var img = new Image<Rgba32>(4000, 3000);
        img.Metadata.ExifProfile = new ExifProfile();
        img.Metadata.ExifProfile.SetValue(ExifTag.GPSLatitudeRef, "N");
        img.Metadata.ExifProfile.SetValue(ExifTag.Make, "Olympus");
        using var ms = new MemoryStream();
        await img.SaveAsJpegAsync(ms);
        ms.Position = 0;

        var a = await new AttachmentReader().ReadAsync("foto.jpg", "image/jpeg", ms, default);

        a.Kind.Should().Be(AttachmentKind.Image);
        using var back = Image.Load(a.Data);
        Math.Max(back.Width, back.Height).Should().Be(AttachmentReader.MaxImageSide);
        back.Metadata.ExifProfile.Should().BeNull("no GPS or camera data may leave the visitor");
    }

    [Test]
    public async Task Word_files_become_text()
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
            doc.AddMainDocumentPart().Document = new W.Document(new W.Body(
                new W.Paragraph(new W.Run(new W.Text("Første linje"))), new W.Paragraph(new W.Run(new W.Text("Anden linje")))));
        ms.Position = 0;
        var a = await new AttachmentReader().ReadAsync("brev.docx", "", ms, default);
        (a.Kind, a.Text).Should().Be((AttachmentKind.Text, "Første linje\nAnden linje"));
    }

    [Test]
    public async Task Broken_pictures_give_a_helpful_message()
    {
        var read = () => new AttachmentReader().ReadAsync("x.jpg", "image/jpeg", new MemoryStream([1, 2, 3]), default);
        await read.Should().ThrowAsync<NotatskriverException>().WithMessage("*Mest kompatibel*");
    }
}

public class WordExporterTests
{
    [Test]
    public void Exports_one_paragraph_per_line_with_bold_headings()
    {
        var bytes = new WordExporter().Export("Mødereferat\n\nVi køber en printer.");
        using var doc = WordprocessingDocument.Open(new MemoryStream(bytes), false);
        var paras = doc.MainDocumentPart!.Document.Body!.Elements<W.Paragraph>().ToList();
        paras.Select(p => p.InnerText).Should().Equal("Mødereferat", "", "Vi køber en printer.");
        paras[0].Descendants<W.Bold>().Should().NotBeEmpty();
        paras[2].Descendants<W.Bold>().Should().BeEmpty();
    }
}

public class DailyAiBudgetTests
{
    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Test]
    public void Stops_at_the_daily_ceiling_and_opens_again_tomorrow()
    {
        var clock = new Clock(new DateTimeOffset(2026, 10, 8, 10, 0, 0, TimeSpan.Zero));
        var budget = new DailyAiBudget(2, clock, NullLogger<DailyAiBudget>.Instance);
        (budget.TryTake("a"), budget.TryTake("b"), budget.TryTake("c")).Should().Be((true, true, false));
        clock.Now = clock.Now.AddDays(1);
        budget.TryTake("d").Should().BeTrue();
    }
}

public class PromptTests
{
    [Test]
    public void The_prompt_carries_type_wish_files_and_notes_and_says_when_pictures_come_first()
    {
        var r = new WriteRequest(DocumentType.All.First(t => t.Key == "mail"), "husk printer",
            [Attachment.Image("s.png", [1]), Attachment.FromText("f.txt", "fra filen")], "kort");
        var p = ClaudeDocumentWriter.Prompt(r);
        p.Should().StartWith("Lav: Mail.").And.Contain("Ekstra ønske fra brugeren: kort")
            .And.Contain("Billederne/PDF'erne ovenfor").And.Contain("<fil navn=\"f.txt\">").And.Contain("<noter>\nhusk printer");
    }

    [Test]
    public void The_system_prompt_asks_for_the_same_block_word_the_domain_checks() =>
        ClaudeDocumentWriter.SystemPrompt.Should().Contain(WrittenDocument.BlockedMarker);
}

using FluentAssertions;
using ITMartinLager.Domain;
using ITMartinLager.Infrastructure.External;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ITMartinLager.Tests;

// The free picture matching (user 2026-11-04: "what about matching pictures?", chose free).
public class CoverPrintTests
{
    private static byte[] Cover(Rgb24 background, Rgb24 band, int bandTop, int width = 300, int height = 400)
    {
        using var img = new Image<Rgb24>(width, height, background);
        img.ProcessPixelRows(rows =>
        {
            for (var y = bandTop; y < Math.Min(height, bandTop + height / 5); y++)
                rows.GetRowSpan(y).Fill(band);
        });
        using var ms = new MemoryStream();
        img.SaveAsJpeg(ms);
        return ms.ToArray();
    }

    private static readonly CoverPrinter Printer = new(new HttpClient(), NullLogger<CoverPrinter>.Instance);

    [Test]
    public void The_same_cover_scores_high_and_another_colour_lower()
    {
        var purple = new Rgb24(120, 40, 110);
        var ours = Printer.Of(Cover(purple, new Rgb24(240, 200, 60), 80))!;
        var sameBigger = Printer.Of(Cover(purple, new Rgb24(240, 200, 60), 160, 600, 800))!;   // the same jacket, photographed larger
        var laterGreen = Printer.Of(Cover(new Rgb24(40, 140, 60), new Rgb24(240, 240, 240), 260))!;
        CoverPrints.Similarity(ours, ours).Should().Be(100);
        CoverPrints.Similarity(ours, sameBigger).Should().BeGreaterThanOrEqualTo(CoverPrints.AlikeFrom);
        CoverPrints.Similarity(ours, laterGreen).Should().BeLessThan(CoverPrints.AlikeFrom);
    }

    [Test]
    public void Bad_pictures_give_no_fingerprint_and_only_priced_things_are_compared()
    {
        Printer.Of([1, 2, 3]).Should().BeNull();
        CoverPrints.Worth(new Item { PriceHint = 120, ItemPhoto = "photos/items/1.jpg" }).Should().BeTrue();
        CoverPrints.Worth(new Item { PriceHint = 30, ItemPhoto = "photos/items/1.jpg" }).Should().BeFalse("ordinary things are not compared");
        CoverPrints.Worth(new Item { PriceHint = 120, Price = 110, ItemPhoto = "x.jpg" }).Should().BeFalse("a person has set the price");
        CoverPrints.Worth(new Item { PriceHint = 120 }).Should().BeFalse("no picture of ours");
    }

    [Test]
    public void The_listing_picture_comes_with_the_listing()
    {
        const string html = """
            <script type="application/ld+json">{"@type":"ItemList","itemListElement":[{"@type":"ListItem","item":{"name":"Tove Ditlevsen: Ansigterne",
            "url":"https://www.dba.dk/1","image":"https://images.dbastatic.dk/a.jpg","offers":{"price":"60","priceCurrency":"DKK"}}}]}</script>
            """;
        var found = ListingParse.Parse(html, "Ansigterne", "Tove Ditlevsen", "https://www.dba.dk/s", _ => 1m);
        found.All.Single().Image.Should().Be("https://images.dbastatic.dk/a.jpg");
    }
}

using Microsoft.Extensions.DependencyInjection;
using FluentAssertions;
using ITMartinLager.Application;
using ITMartinLager.Domain;
using ITMartinLager.Infrastructure.External;

namespace ITMartinLager.Tests;

public class BarcodeTests
{
    [TestCase("Gran Turismo 3 A-Spec (PS2)", "Konsolspil", "PlayStation 2")]
    [TestCase("Super Mario 64 - Nintendo 64", "Konsolspil", "Nintendo 64")]
    [TestCase("Halo 3 Xbox 360", "Konsolspil", "Xbox 360")]
    [TestCase("The Matrix [Blu-ray]", "Blu-ray", "")]
    [TestCase("Ringenes Herre DVD", "DVD", "")]
    [TestCase("Elton John - Greatest Hits (CD)", "CD", "")]
    [TestCase("Something else", "Andet", "")]
    public void Kind_and_console_are_guessed_from_the_product_title(string title, string kind, string platform) =>
        BarcodeLookup.Guess(title).Should().Be((kind, platform));

    [Test]
    public void Isbn_is_978_or_979_with_13_digits_and_scanner_noise_is_removed()
    {
        Barcodes.Clean(" 978-0-14-044913-6 ").Should().Be("9780140449136");
        Barcodes.IsIsbn("9780140449136").Should().BeTrue();
        Barcodes.IsIsbn("5051892004521").Should().BeFalse();
        Barcodes.IsIsbn("978014044913").Should().BeFalse();
    }
}

// Discogs barcode search (2026-10-21): it knew the Frank Sinatra CD the other free sources had only as a title.
public class DiscogsBarcodeTests
{
    [Test]
    public void The_title_splits_into_artist_and_title()
    {
        BarcodeLookup.SplitDiscogsTitle("Frank Sinatra - Frank Sinatra").Should().Be(("Frank Sinatra", "Frank Sinatra"));
        BarcodeLookup.SplitDiscogsTitle("Gnags - Mr. Swing King - Live").Should().Be(("Gnags", "Mr. Swing King - Live"));
        BarcodeLookup.SplitDiscogsTitle("Untitled").Should().Be(("", "Untitled"));
    }

    [TestCase(new[] { "CD", "Compilation", "Remastered" }, "CD")]
    [TestCase(new[] { "Vinyl", "LP", "Album" }, "Vinyl")]
    [TestCase(new[] { "Cassette", "Album" }, "Kassettebånd")]
    [TestCase(new[] { "DVD", "PAL" }, "DVD")]
    [TestCase(new[] { "Blu-ray", "DVD" }, "Blu-ray")]
    [TestCase(new[] { "File" }, "Andet")]
    public void The_format_gives_the_kind(string[] formats, string kind) => BarcodeLookup.DiscogsKind(formats).Should().Be(kind);
}

// Vinyl and tapes are searched as vinyl and cassettes on Discogs (2026-10-21), one source per format.
public class DiscogsFormatTests
{
    private static List<ITMartinLager.Application.IPriceSource> Sources()
    {
        var services = new ServiceCollection().AddLogging();
        ITMartinLager.Infrastructure.PriceSites.PriceSourceRegistration.AddPriceSources(services);
        return services.BuildServiceProvider().GetServices<ITMartinLager.Application.IPriceSource>().ToList();
    }

    [Test, Explicit, Category("Live")]
    public async Task Live_a_film_searched_with_its_format()
    {
        foreach (var title in new[] { "Guardians of the Galaxy", "Underworld Rise of the Lycans" })
        {
            var item = new ITMartinLager.Domain.Item { Kind = Kinds.Dvd, Title = title };
            foreach (var s in Sources().Where(s => s.Name is "DBA" or "GulogGratis"))
            {
                var r = await s.LookupAsync(title, ITMartinLager.Domain.Pricing.SearchFormat(item.Kind), "", CancellationToken.None);
                if (r is null) { TestContext.Out.WriteLine($"{title} | {s.Name} | no answer"); continue; }
                var kept = ITMartinLager.Domain.Pricing.OnlyThisItem(item, r, s.Name, searchedWithFormat: true);
                TestContext.Out.WriteLine($"{title} | {s.Name} | {r.All.Count} found, {kept.All.Count} kept");
                foreach (var o in kept.All.Take(6)) TestContext.Out.WriteLine($"    {o.Price} | {o.Name}");
            }
        }
    }

    // 2026-10-25 (user): "Danish, Tradera and One from ebay" - nothing else is searched.
    [Test]
    public void Only_the_Danish_sites_Tradera_and_one_eBay_are_searched() =>
        Sources().Select(s => s.Name).Distinct().Should().BeEquivalentTo(
            ["DBA", "GulogGratis", "eBay", "Discogs", "Accord", "Tradera", "Nemos Bibliotek", "Faraos", "Fantask", "Bog & idé"]);
}

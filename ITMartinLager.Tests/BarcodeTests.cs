using FluentAssertions;
using ITMartinLager.Application;
using ITMartinLager.Domain;
using ITMartinLager.Infrastructure.External;
using ITMartinLager.Infrastructure.Persistence;
using ITMartinLager.Infrastructure.PriceSites;

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

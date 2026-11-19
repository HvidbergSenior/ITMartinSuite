using FluentAssertions;
using ITMartin.Receipt.Application.Services;
using NUnit.Framework;

namespace ITMartin.Receipt.Tests.Services;

// 2026-11-19: uploaded pictures = receipts or offer screenshots, read for free (OCR + rules).
public class PictureReaderTests
{
    private const string Lidl = """
        LIDL DANMARK K/S
        Skejbyvej 10, 8200 Aarhus N
        BKI KAFFE FORMALET     39,95 A
        Lidl Plus rabat        -9,95
        MÆLK 1L                12,95 A
        2 x 18,00
        RUGBRØD                36,00 A
        RABAT                  -5,00
        Pant                    1,50
        SUBTOTAL               85,45
        TOTAL                  85,45
        DANKORT                85,45
        MOMS                   17,09
        18.11.2026 14:32
        """;

    [Test]
    public void A_receipt_gives_lines_with_their_discounts()
    {
        var r = PictureReader.Read(Lidl);
        r.Kind.Should().Be(PictureKind.Kvittering);
        r.Facts.Total.Should().Be(85.45m);
        r.Lines.Select(l => l.Description).Should().Equal("BKI KAFFE FORMALET", "MÆLK 1L", "RUGBRØD", "Pant");
        r.Lines[0].Discount.Should().Be(9.95m);
        r.Lines[0].DiscountLabel.Should().Be("Lidl Plus rabat");
        r.Lines[0].Paid.Should().Be(30m);
        r.Lines[2].Paid.Should().Be(31m);
    }

    [Test]
    public void A_lidl_plus_screenshot_is_an_offer_for_the_list()
    {
        const string shot = """
            Lidl Plus
            Kuponer
            BKI Kaffe formalet
            400 g
            24,95
            Pr. kg 62,38
            Gælder til 24.11
            Aktivér kupon
            """;
        var r = PictureReader.Read(shot);
        r.Kind.Should().Be(PictureKind.Tilbud);
        r.Offer!.Shop.Should().Be("Lidl");
        r.Offer.Product.Should().Be("BKI Kaffe formalet");
        r.Offer.Price.Should().Be(24.95m);
        r.Offer.AppOnly.Should().BeTrue();
        r.Offer.App.Should().Be("Lidl Plus");
    }

    [Test]
    public void The_product_line_is_the_text_above_the_price_not_the_size()
    {
        const string shot = "Netto\nUgens tilbud\nArla Lærkevang mælk\n15,-\nFør 18,95\n";
        var o = PictureReader.Read(shot).Offer!;
        o.Product.Should().Be("Arla Lærkevang mælk");
        o.Price.Should().Be(15m);
        o.Shop.Should().Be("Netto");
        o.AppOnly.Should().BeFalse();
    }

    [Test]
    public void A_photo_with_no_prices_is_unknown() =>
        PictureReader.Read("Hej med dig\nVi ses i morgen").Kind.Should().Be(PictureKind.Ukendt);
}

public class DiscountCheckTests
{
    private sealed class Offers(params OfferInfo[] list) : IOfferLookup
    {
        public int Calls;
        public Task<IReadOnlyList<OfferInfo>> SearchAsync(string q, double lat, double lng, CancellationToken ct = default) { Calls++; return Task.FromResult<IReadOnlyList<OfferInfo>>(list); }
    }

    private static readonly DateTime Day = new(2026, 11, 18);
    private static OfferInfo O(string chain, string title, decimal price, string? app = null) =>
        new(chain, title, price, new DateTimeOffset(Day.AddDays(-3)), new DateTimeOffset(Day.AddDays(3)), app, null);

    [Test]
    public async Task Paying_more_than_the_shops_offer_gives_a_tjek_note()
    {
        var check = new DiscountCheck(new Offers(O("Lidl", "BKI formalet kaffe", 30m), O("Netto", "BKI formalet kaffe", 25m)));
        var notes = await check.CheckAsync("lidl", Day, [new ReceiptLine("BKI KAFFE FORMALET", 39.95m)], 56, 10);
        notes.Should().ContainSingle().Which.Text.Should().Contain("tilbud 30 kr hos Lidl").And.Contain("39,95");
    }

    [Test]
    public async Task The_discount_on_the_receipt_counts_and_other_shops_do_not()
    {
        var check = new DiscountCheck(new Offers(O("Lidl", "BKI formalet kaffe", 30m), O("Netto", "BKI formalet kaffe", 25m)));
        (await check.CheckAsync("lidl", Day, [new ReceiptLine("BKI KAFFE FORMALET", 39.95m, 9.95m)], 56, 10)).Should().BeEmpty();
    }

    [Test]
    public async Task An_offer_not_valid_on_the_day_or_another_product_is_ignored()
    {
        var old = new OfferInfo("Lidl", "BKI formalet kaffe", 20m, new DateTimeOffset(Day.AddDays(-20)), new DateTimeOffset(Day.AddDays(-10)), null, null);
        var check = new DiscountCheck(new Offers(old, O("Lidl", "Gevalia kaffebønner", 20m)));
        (await check.CheckAsync("lidl", Day, [new ReceiptLine("BKI KAFFE FORMALET", 39.95m)], 56, 10)).Should().BeEmpty();
    }

    [Test]
    public async Task At_most_twelve_lookups_per_receipt()
    {
        var offers = new Offers();
        var lines = Enumerable.Range(0, 30).Select(i => new ReceiptLine($"VARE NUMMER{i}", 20m)).ToList();
        await new DiscountCheck(offers).CheckAsync("lidl", Day, lines, 56, 10);
        offers.Calls.Should().Be(DiscountCheck.MaxLookups);
    }
}

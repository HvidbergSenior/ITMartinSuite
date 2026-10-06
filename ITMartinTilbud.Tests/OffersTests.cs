using System.Text.Json;
using FluentAssertions;
using ITMartinTilbud.Server.Services;

namespace ITMartinTilbud.Tests;

// Tjek offer JSON as captured 2026-10-06 (trimmed): price per kg/litre, grocery-only filter, expired offers, sort order.
public class OffersTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    private static string O(string chain, decimal price, string? unit, decimal? size, decimal pieces = 1, decimal? sizeTo = null,
        string till = "2026-10-09T21:59:59+0000", decimal factor = 1, string? siSymbol = null)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        string N(decimal? d) => d?.ToString(inv) ?? "null";
        var quantity = unit is null ? "null"
            : "{\"unit\":{\"symbol\":\"" + unit + "\",\"si\":{\"symbol\":\"" + (siSymbol ?? unit) + "\",\"factor\":" + N(factor) + "}}," +
              "\"size\":{\"from\":" + N(size) + ",\"to\":" + N(sizeTo ?? size) + "},\"pieces\":{\"from\":" + N(pieces) + ",\"to\":" + N(pieces) + "}}";
        return "{\"heading\":\"Vare\",\"dealer\":{\"name\":\"" + chain + "\"},\"pricing\":{\"price\":" + N(price) + ",\"pre_price\":null}," +
               "\"quantity\":" + quantity + ",\"run_from\":\"2026-10-02T22:00:00+0000\",\"run_till\":\"" + till + "\"}";
    }

    private static List<Offer> Parse(params string[] offers) =>
        TjekOffers.Parse(JsonDocument.Parse("[" + string.Join(",", offers) + "]").RootElement, Now);

    [Test]
    public void Coffee_3_pack_of_400_g_is_priced_per_kg()
    {
        var o = Parse(O("Lidl", 129, "kg", 1.2m)).Single();
        o.UnitPrice.Should().Be(107.50m);
        o.UnitLabel.Should().Be("kr/kg");
    }

    [Test]
    public void Grams_use_the_si_factor_and_pieces()
    {
        // 2 x 500 g = 1 kg for 50 kr
        var o = Parse(O("Netto", 50, "g", 500, pieces: 2, factor: 0.001m, siSymbol: "kg")).Single();
        o.UnitPrice.Should().Be(50m);
        o.Size.Should().Be("2 × 500 g");
    }

    [Test]
    public void A_size_range_is_priced_on_the_larger_size()
    {
        var o = Parse(O("REMA 1000", 20, "g", 400, sizeTo: 500, factor: 0.001m, siSymbol: "kg")).Single();
        o.UnitPrice.Should().Be(40m);
    }

    [Test]
    public void Non_grocery_chains_and_expired_offers_are_left_out()
    {
        Parse(O("POWER", 99, null, null), O("føtex", 10, null, null, till: "2026-10-01T21:59:59+0000")).Should().BeEmpty();
    }

    [Test]
    public void Loose_matches_are_dropped_when_enough_titles_match()
    {
        Offer Of(string title, string? text = null) => new("Lidl", title, text, 10, null, null, null, null, null, null, null, null);
        var list = new List<Offer> { Of("Kartofler", "Gode med smør"), Of("Lurpak smør"), Of("Kærgården smørbar"), Of("Smør fra Arla") };
        TjekOffers.Relevant(list, "smør").Select(o => o.Title).Should().Equal("Lurpak smør", "Kærgården smørbar", "Smør fra Arla");
        TjekOffers.Relevant(list, "æbler").Should().HaveCount(4);   // no word match at all: show what was found
    }

    [Test]
    public void Milk_drops_shower_gel_and_puts_cocoa_milk_last()
    {
        Offer Of(string title) => new("Netto", title, null, 10, null, null, null, null, null, null, null, null);
        var list = new List<Offer>
        {
            Of("MILBONA Kakaomælk"), Of("Shower Gel m. orkidé & mælk"), Of("Arla Lactofree mælk"),
            Of("Matilde kakao-skummetmælk"), Of("Gram Slot Mini- eller skummetmælk"), Of("Arla frisk dansk mælk"),
        };
        var r = TjekOffers.Relevant(list, "mælk");
        r.Where(o => !o.Variant).Select(o => o.Title).Should().Equal("Arla Lactofree mælk", "Gram Slot Mini- eller skummetmælk", "Arla frisk dansk mælk");
        r.Where(o => o.Variant).Select(o => o.Title).Should().Equal("MILBONA Kakaomælk", "Matilde kakao-skummetmælk");
        TjekOffers.Relevant(list, "kakaomælk").Should().OnlyContain(o => !o.Variant);   // searched for the flavour itself
    }

    [Test]
    public void App_only_offers_are_recognised_as_the_leaflets_word_them()
    {
        TjekOffers.AppNeeded("Black Coffee | + PRIS 59:- Gælder kun med Netto+ appen 400 g.", "Netto").Should().Be("Netto+ appen");
        TjekOffers.AppNeeded("Cheasy hytteost | plus pris Pr. kg 40,- Gælder kun med føtex plus appen", "føtex").Should().Be("Føtex plus appen");
        TjekOffers.AppNeeded("MATILDE Kakaoskummetmælk | Fredagsdeal Kuponpris ved køb på min. 200 kr. Lidl Plus", "Lidl").Should().Be("Lidl Plus-appen");
        TjekOffers.AppNeeded("Arla letmælk 1 l. Pr. l 9,95", "Netto").Should().BeNull();
        TjekOffers.ConditionOf("Kuponpris ved køb på min. 200 kr.").Should().Be("kræver køb for min. 200 kr");
    }

    [Test]
    public void App_prices_as_the_week_41_leaflets_print_them()
    {
        // Netto: the price shown (24) is for everyone, the app gives 20.
        TjekOffers.AppInfo("Riberhus skiveost 200-240 g. Pr. kg max. 120,00 + PRIS 20: Pr. kg max. 100,00 Gælder kun med Netto+ appen", "Netto", 24, 120)
            .Should().Be(("Netto+ appen", false, 20m, (decimal?)null));
        // føtex: two kg prices, normal first - 15 kr for everyone, about 10 kr with the app.
        TjekOffers.AppInfo("Pågen Gifflar 280-800 g. Pr. kg max. 57,69 plus pris Pr. kg max 38,46 Gælder kun med føtex plus appen", "føtex", 15, 57.69m)
            .Should().Be(("Føtex plus appen", false, 10m, (decimal?)null));
        // Bilka: explicit plus price.
        TjekOffers.AppInfo("Hakket oksekød 900-2500 g. Pr. kg max. 116.67 FRIT VALG PLUS PRIS FRIT VALG 89.- Pr. kg max. 98.89 GÆLDER KUN MED BILKA PLUS APPEN", "Bilka", 105, null)
            .Should().Be(("Bilka plus appen", false, 89m, (decimal?)null));
        // Netto, a range of sizes: our per-litre price (on the big bottle) must not decide - the printed order does.
        TjekOffers.AppInfo("Sierra Tequila 50-70 cl. Pr. liter max. 218,00 + PRIS Pr. liter max 198,00 Gælder kun med Netto+ appen", "Netto", 109, 155.71m)
            .Should().Be(("Netto+ appen", false, 99m, (decimal?)null));
        TjekOffers.AppInfo("Hakket oksekød 900-2500 g. Pr. kg max. 116.67 PLUS PRIS FRIT VALG 89.- Pr. kg max. 98.89", "Bilka", 105, 42m)
            .Should().Be(("Bilkas app", false, 89m, (decimal?)null));
        // Kvickly: member and non-member price both printed.
        TjekOffers.AppInfo("Hat. Pris ikke-medlem 79,95. Medlemspris 55,97.", "Kvickly", 79.95m, null)
            .Should().Be(("Coop-appen (medlem)", false, 55.97m, (decimal?)null));
        // Lidl coupon: the price shown is the app price, the normal one is not printed.
        TjekOffers.AppInfo("MATILDE Kakaoskummetmælk 1 l. Pr. l 7,00 Kuponpris ved køb på min. 200 kr. Lidl Plus", "Lidl", 7, 7)
            .Should().Be(("Lidl Plus-appen", true, (decimal?)null, (decimal?)null));
    }

    [Test]
    public void App_gap_adds_up_what_you_pay_extra_without_the_app()
    {
        Offer Of(decimal price, decimal? appPrice, string? app) => new("Netto", "x", null, price, null, null, null, null, null, null, null, null, AppName: app, AppPrice: appPrice);
        var g = AppGap.Summarise("Netto", [Of(24, 20, "Netto+ appen"), Of(109, 99, "Netto+ appen"), Of(10, null, null), Of(50, null, "Netto+ appen")], "uge 41");
        g.Offers.Should().Be(4); g.AppOffers.Should().Be(3); g.Priced.Should().Be(2);
        g.ExtraTotal.Should().Be(14m); g.ExtraAverage.Should().Be(7m);
    }

    [Test]
    public void A_friday_deal_counts_only_on_its_day()
    {
        var d = TjekOffers.OnlyDay("Fredagsdeal Gælder kun 9. okt.", new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero));
        d!.Value.Date.Should().Be(new DateTime(2026, 10, 9));
        TjekOffers.OnlyDay("Gælder hele ugen", DateTimeOffset.UtcNow).Should().BeNull();
    }

    [Test]
    public void The_last_word_is_the_thing_and_the_words_before_put_that_kind_first()
    {
        Offer Of(string title, string? text = null) => new("Netto", title, text, 10, null, null, null, null, null, null, null, null);
        var list = new List<Offer> { Of("Arla Lactofree mælk"), Of("Thise økologisk kefir", "1 l"), Of("Øko letmælk", "1 l"), Of("Arla frisk dansk mælk", "Økologisk") };
        var r = TjekOffers.Relevant(list, "økologisk mælk");
        r.Select(o => o.Title).Should().Equal("Øko letmælk", "Arla frisk dansk mælk", "Arla Lactofree mælk");   // kefir is not milk
        r.Where(o => o.Matches).Should().HaveCount(2);
    }

    [Test]
    public void Capsules_per_piece_do_not_beat_coffee_per_kg()
    {
        var list = Parse(O("Bilka", 25, "pcs", 8), O("Lidl", 60, "kg", 0.5m), O("MENY", 40, "kg", 0.5m));
        list.Select(x => x.Chain).Should().Equal("MENY", "Lidl", "Bilka");
        list.Last().UnitLabel.Should().Be("kr/stk");
    }

    [Test]
    public void Cheapest_per_kg_first_and_offers_without_size_last()
    {
        var list = Parse(O("Bilka", 5, null, null), O("Lidl", 60, "kg", 0.5m), O("MENY", 40, "kg", 0.5m));
        list.Select(x => x.Chain).Should().Equal("MENY", "Lidl", "Bilka");
    }
}

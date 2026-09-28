using FluentAssertions;
using ITMartinElPriser.Core;
using NUnit.Framework;

namespace ITMartinElPriser.Tests;

[TestFixture]
public class ElBillParserTests
{
    // The text PdfPig extracts from an NRGi bill (Aug 2026), name and address replaced.
    private const string Nrgi = """
        Elregning
        Kunde Kundesen
        Eksempelvej 1
        8200 Aarhus N
        Fakturadato 17.09.2026
        268,791 kWh 483,03 kr.Du har brugt
        Din opgørelse for PERIODEN: 01.08.2026 - 31.08.2026
        483,04 kr.Du skal i alt betale ( inkl. moms )
        Detaljeret elregning
        Din opgørelse for PERIODEN: 01.08.2026 - 31.08.2026
        kWh kr.Dit elselskab NRGi Elhandel A/S
        Strømpris 268,791  x  1,10717 297,60 kr.
        Pristillæg 268,791  x  0,08998 24,18 kr.
        Abonnement til NRGi Elhandel A/S 29,00 kr.
        Dit netselskab Konstant Net A/S
        Rabat på Nettarif C - KONSTANT Net A/S 268,791  x  -0,02808 -7,55 kr.
        Nettarif C 268,791  x  0,15361 41,29 kr.
        Rabat på Net abo C forbrug - KONSTANT Net A/S -8,44 kr.
        Net abo C forbrug 46,15 kr.
        Staten
        Elafgift 268,791  x  0,01000 2,69 kr.
        Systemtarif 268,791  x  0,09000 24,19 kr.
        Transmissions nettarif 268,791  x  0,05375 14,45 kr.
        TSO - System Abonnement 19,48 kr.
        I alt for perioden inkl. abonnementer og faste gebyrer
        483,03 kr.
        Målernr.:
        Aftagenr.:
        10095897
        571313115101585625
        Du har en elmåler fra Konstant Net A/S
        Du har en elaftale med NRGi Elhandel A/S
        Dit produkt er: NRGi Time
        """;

    [Test]
    public void Reads_the_parties_product_period_and_totals()
    {
        var b = ElBillParser.Parse(Nrgi)!;
        b.Supplier.Should().Be("NRGi Elhandel A/S");
        b.Grid.Should().Be("Konstant Net A/S");
        b.Product.Should().Be("NRGi Time");
        b.From.Should().Be(new DateOnly(2026, 8, 1));
        b.To.Should().Be(new DateOnly(2026, 8, 31));
        b.Days.Should().Be(31);
        b.InvoiceDate.Should().Be(new DateOnly(2026, 9, 17));
        b.Kwh.Should().BeApproximately(268.791, 1e-9);
        b.TotalKr.Should().Be(483.04);
        b.MeterNumber.Should().Be("10095897");
        b.MeteringPointId.Should().Be("571313115101585625");
    }

    [Test]
    public void Reads_every_line_into_the_right_party()
    {
        var b = ElBillParser.Parse(Nrgi)!;
        b.Lines.Should().HaveCount(11);
        b.Lines.Count(l => l.Party == BillParty.Supplier).Should().Be(3);
        b.Lines.Count(l => l.Party == BillParty.Grid).Should().Be(4);
        b.Lines.Count(l => l.Party == BillParty.State).Should().Be(4);
        b.Lines.Sum(l => l.Kr).Should().BeApproximately(483.03, 0.02);
        b.PartKr(BillParty.Supplier).Should().BeApproximately(350.78, 0.01);

        var rabat = b.Lines.Single(l => l.Name.StartsWith("Rabat på Nettarif"));
        rabat.RateKr.Should().Be(-0.02808);
        rabat.Kr.Should().Be(-7.55);
    }

    [Test]
    public void Finds_the_suppliers_markup_and_subscription()
    {
        var b = ElBillParser.Parse(Nrgi)!;
        b.MarkupKrPerKwh.Should().Be(0.08998);
        b.SubscriptionKr.Should().Be(29.00);
        b.YearlyKwhEstimate.Should().BeApproximately(268.791 * 365 / 31, 0.01);
    }

    [Test]
    public void Every_line_gets_an_explanation()
    {
        var b = ElBillParser.Parse(Nrgi)!;
        b.Lines.Should().OnlyContain(l => ElBillParser.Explain(l).Length > 20);
    }

    [Test]
    public void Text_that_is_not_a_bill_gives_null()
    {
        ElBillParser.Parse("Kære Martin\nTak for sidst.").Should().BeNull();
    }
}

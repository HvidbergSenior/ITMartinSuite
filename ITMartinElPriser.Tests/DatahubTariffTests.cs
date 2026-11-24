using FluentAssertions;
using ITMartinElPriser.Domain;
using ITMartinElPriser.Infrastructure;

namespace ITMartinElPriser.Tests;

// Energinet DatahubPricelist rows as captured 2026-11-24 (Konstant Net A/S - 151, winter 2026/27).
[TestFixture]
public class DatahubTariffTests
{
    private static double?[] Day(double night, double day, double peak) =>
        [.. Enumerable.Range(0, 24).Select(h => (double?)(h < 6 ? night : h is >= 17 and < 21 ? peak : day))];

    private static DatahubTariffBook.Record Rec(string owner, string note, string from, string? to, double?[] prices, string code = "X")
    {
        var r = new DatahubTariffBook.Record { ChargeOwner = owner, Note = note, ChargeTypeCode = code, ValidFrom = DateTime.Parse(from), ValidTo = to is null ? null : DateTime.Parse(to) };
        var p = typeof(DatahubTariffBook.Record).GetProperties().Where(x => x.Name.StartsWith('P') && x.Name.Length <= 3).ToList();
        for (var i = 0; i < 24; i++) p.Single(x => x.Name == $"P{i + 1}").SetValue(r, prices[i]);
        return r;
    }

    private static DatahubTariffBook Book() => new(DatahubTariffBook.Household(
    [
        Rec("Konstant Net A/S - 151", "Nettarif C", "2026-10-01", "2027-02-01", Day(0.0605, 0.1816, 0.5449), "C_FBTNTR_B"),
        Rec("Konstant Net A/S - 151", "Nettarif C", "2026-04-01", "2026-10-01", Day(0.0605, 0.0908, 0.2361), "C_FBTNTR_B"),
        Rec("Konstant Net A/S - 151", "Rabat Nettarif C -  KONSTANT Net A/S", "2026-11-01", "2027-01-01", Day(-0.0111, -0.0332, -0.0996), "C_FBTNTR_R"),
        Rec("Konstant Net A/S - 151", "Nettarif C lokal kollektiv", "2026-11-01", "2027-02-01", Day(1, 1, 1), "C_FBTLKTFB"),
        Rec("Konstant Net A/S - 151", "Nettarif B lav", "2026-11-23", "2026-11-28", Day(1, 1, 1), "BLFBTNTR_B"),
        Rec("Konstant Net A/S - 245", "Nettarif C", "2026-10-01", "2027-02-01", Day(0.0605, 0.1816, 0.5449)),
        Rec("Cerius A/S", "Nettarif C", "2026-10-01", null, Day(0.2, 0.4, 1.038025)),
        Rec("Elinord A/S", "Nettarif C", "2026-01-01", null, [0.30, .. Enumerable.Repeat<double?>(null, 23)]),
        Rec("Aal El-Net A M B A", "Nettarif C Produktion", "2026-10-01", null, Day(9, 9, 9)),
    ]));

    [Test]
    public void Household_pays_nettarif_C_plus_its_rebate()
    {
        var b = Book();
        b.At("Konstant Net A/S - 151", new DateTime(2026, 11, 24, 18, 0, 0)).Should().BeApproximately(0.5449 - 0.0996, 1e-9);
        b.At("Konstant Net A/S - 151", new DateTime(2026, 11, 24, 3, 0, 0)).Should().BeApproximately(0.0605 - 0.0111, 1e-9);
    }

    [Test]
    public void Summer_rows_and_other_customer_types_are_not_mixed_in()
    {
        var b = Book();
        b.At("Konstant Net A/S - 151", new DateTime(2026, 7, 1, 18, 0, 0)).Should().BeApproximately(0.2361, 1e-9);
        b.At("Aal El-Net A M B A", new DateTime(2026, 11, 24, 18, 0, 0)).Should().BeNull();
    }

    [Test]
    public void A_row_with_only_price1_is_the_same_all_day_and_past_the_last_row_the_latest_is_used()
    {
        var b = Book();
        b.At("Elinord A/S", new DateTime(2026, 11, 24, 18, 0, 0)).Should().Be(0.30);
        b.At("Konstant Net A/S - 245", new DateTime(2027, 3, 1, 18, 0, 0)).Should().BeApproximately(0.5449, 1e-9);
    }

    [Test]
    public void Green_Power_Denmarks_answer_finds_the_owner_by_area_number_or_name()
    {
        var b = Book();
        b.OwnerFor("151", "KONSTANT Net A/S").Should().Be("Konstant Net A/S - 151");
        b.OwnerFor("245", "KONSTANT Net A/S").Should().Be("Konstant Net A/S - 245");
        b.OwnerFor("740", "Cerius A/S").Should().Be("Cerius A/S");
        b.OwnerFor("999", "Ukendt Net A/S").Should().BeNull();
    }

    [Test]
    public void Old_stromligning_ids_are_matched_from_the_saved_short_name()
    {
        GridIds.IsLegacy("konstant_c").Should().BeTrue();
        GridIds.IsLegacy("Konstant Net A/S - 151").Should().BeFalse();
        Book().OwnerForShortName("Cerius").Should().Be("Cerius A/S");
        Book().OwnerForShortName("Konstant").Should().StartWith("Konstant Net A/S - ");
    }

    [TestCase("DK-85", "DK2")]
    [TestCase("DK-84", "DK2")]
    [TestCase("DK-82", "DK1")]
    [TestCase("", "DK1")]
    public void Price_area_comes_from_the_region(string region, string area) =>
        GridTariffs.AreaOf(new Place("x", "8000", 56, 10, region)).Should().Be(area);
}

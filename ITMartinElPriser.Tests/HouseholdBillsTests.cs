using FluentAssertions;
using ITMartinElPriser.Core;
using ITMartinMitEl.Server.Services;
using NUnit.Framework;

namespace ITMartinElPriser.Tests;

// HouseholdStore.Parse falls back to an EMPTY household on any JsonException, so a bill that
// cannot be read back would silently wipe a household's settings, devices and push subscribers.
[TestFixture]
public class HouseholdBillsTests
{
    [Test]
    public void A_saved_bill_reads_back_and_keeps_the_rest_of_the_household()
    {
        string saved = "";
        var store = new HouseholdStore(Guid.NewGuid(), null, (_, json) => saved = json);
        var bill = new ElBill("NRGi Elhandel A/S", "NRGi Time", "Konstant Net A/S",
            new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), new DateOnly(2026, 9, 17),
            268.791, 483.04,
            [new ElBillLine(BillParty.Supplier, "Pristillæg", 268.791, 0.08998, 24.18),
             new ElBillLine(BillParty.Grid, "Net abo C forbrug", null, null, 46.15)],
            "10095897", "571313115101585625", new DateTime(2026, 9, 28, 10, 0, 0, DateTimeKind.Utc));

        store.Update(d => { d.Settings.GridName = "Konstant"; d.Bills.Insert(0, bill); });

        var back = HouseholdStore.Parse(saved);
        back.Settings.GridName.Should().Be("Konstant");
        back.Bills.Should().HaveCount(1);
        var b = back.Bills[0];
        b.Supplier.Should().Be("NRGi Elhandel A/S");
        b.From.Should().Be(new DateOnly(2026, 8, 1));
        b.Lines.Should().HaveCount(2);
        b.Lines[0].Party.Should().Be(BillParty.Supplier);
        b.Lines[0].RateKr.Should().Be(0.08998);
        b.Lines[1].Kwh.Should().BeNull();
        b.MarkupKrPerKwh.Should().Be(0.08998);
    }

    [Test]
    public void Old_households_without_bills_still_read()
    {
        HouseholdStore.Parse("""{"Settings":{"GridName":"Konstant"}}""").Bills.Should().BeEmpty();
    }
}

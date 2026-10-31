using FluentAssertions;
using ITMartinSalg.Domain;

namespace ITMartinSalg.Tests;

// The loppemarked sale (user 2026-10-31: "a way to make a sale so that it will disappear in produkter and be registered with price").
public class MarketTests
{
    private static StockItem Item(int qty = 1, int? price = 40) =>
        new(7, Kinds.AndersAnd, "Anders And & Co. 1968 nr. 12", "Anders And & Co.", "", "God", 1968, qty, "Brugt", price, null, 1, false, "");

    [Test]
    public void A_market_sale_is_paid_done_and_tells_Lager_the_price()
    {
        var now = new DateTime(2026, 11, 1, 10, 30, 0, DateTimeKind.Utc);
        var o = Order.AtMarket(Item(), 30, now);
        (o.Channel, o.Status, o.Kr, o.PaidKr, o.Paid, o.NeedsBooking).Should().Be((SaleChannels.Market, OrderStatuses.PickedUp, 30, 30, true, true));
        o.Lines.Single().ListKr.Should().Be(40);
        o.Sale().Lines.Single().Should().Match<StockSaleLine>(l => l.ItemId == 7 && l.Kr == 30 && l.How.StartsWith("på loppemarkedet for 30 kr"));
    }

    [Test]
    public void No_price_or_nothing_left_is_not_a_sale()
    {
        var now = DateTime.UtcNow;
        FluentActions.Invoking(() => Order.AtMarket(Item(), 0, now)).Should().Throw<InvalidOperationException>().WithMessage("Skriv prisen*");
        FluentActions.Invoking(() => Order.AtMarket(Item(qty: 0), 20, now)).Should().Throw<InvalidOperationException>().WithMessage("*allerede solgt*");
    }
}

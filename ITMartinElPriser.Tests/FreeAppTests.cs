using FluentAssertions;
using ITMartinElPriser.Application;
using ITMartinElPriser.Domain;
using Moq;

namespace ITMartinElPriser.Tests;

// "Hvad havde du sparet? is that calculated correct?" (user 2026-10-21) - the front page's saving, and the free app's
// use cases after the DDD/Clean move.
[TestFixture]
public class HindsightTests
{
    private static readonly HouseholdSettings Spot = new() { ShowAllIn = false };
    private static readonly Appliance TwoHours = new() { Name = "Opvask", KwhPerRun = 1.0, DurationHours = 2 };

    // Days of 15-minute prices, spot by hour; summer time (UTC+2) the whole way.
    private static List<PricePoint> Days(DateOnly from, int days, Func<int, double> byHour)
    {
        var list = new List<PricePoint>();
        for (var d = 0; d < days; d++)
            for (var h = 0; h < 24; h++)
                for (var q = 0; q < 4; q++)
                {
                    var t = from.AddDays(d).ToDateTime(new TimeOnly(h, q * 15));
                    list.Add(new PricePoint { TimeDk = t, TimeUtc = t.AddHours(-2), PriceKrPerKwh = byHour(h) });
                }
        return list;
    }

    private static double Evening(int h) => h is >= 18 and < 20 ? 3.0 : h is >= 2 and < 4 ? 0.5 : 1.0;

    [Test]
    public void The_saving_is_18_oclock_against_the_days_cheapest_start_every_day()
    {
        var today = new DateOnly(2026, 6, 30);
        var raw = Days(today.AddDays(-30), 31, Evening);   // the 30 days before today AND today
        var hs = Hindsight.For(raw, Spot, TwoHours, today)!;
        hs.Days.Should().Be(30);
        hs.UsualKr.Should().BeApproximately(30 * 3.0, 0.01);       // 1 kWh at 3 kr every evening
        hs.CheapestKr.Should().BeApproximately(30 * 0.5, 0.01);    // 1 kWh at 0.5 kr every night
        hs.SavedKr.Should().BeApproximately(30 * 2.5, 0.01);
        hs.PerYearKr.Should().BeApproximately(2.5 * 365, 0.1);
    }

    [Test]
    public void A_run_that_ends_after_midnight_is_priced_with_the_next_days_prices()
    {
        // The cheapest two hours are 23:00-01:00. Without the next day's prices that start could not be priced.
        var today = new DateOnly(2026, 6, 10);
        double Late(int h) => h is 23 or 0 ? 0.1 : h is >= 18 and < 20 ? 3.0 : 1.0;
        var withToday = Hindsight.For(Days(today.AddDays(-10), 11, Late), Spot, TwoHours, today)!;
        withToday.CheapestKr.Should().BeApproximately(10 * 0.1, 0.01);
    }

    [Test]
    public void Too_few_days_or_no_18_oclock_price_says_nothing()
    {
        var today = new DateOnly(2026, 6, 10);
        Hindsight.For(Days(today.AddDays(-5), 6, Evening), Spot, TwoHours, today).Should().BeNull();   // 5 days < MinDays
        Hindsight.For([], Spot, TwoHours, today).Should().BeNull();
    }

    [Test]
    public void The_same_as_pricing_each_run_on_its_own()
    {
        // Against the page's own run price (PriceModel.CostOfRun), on a day with varied prices and a 1.5 hour machine.
        var today = new DateOnly(2026, 6, 20);
        double Wavy(int h) => 0.5 + (h * 7 % 11) / 4.0;
        var raw = Days(today.AddDays(-8), 9, Wavy);
        var a = new Appliance { KwhPerRun = 0.9, DurationHours = 1.5 };
        var hs = Hindsight.For(raw, Spot, a, today, days: 8)!;
        double usual = 0, best = 0;
        for (var d = today.AddDays(-8); d < today; d = d.AddDays(1))
        {
            usual += PriceModel.CostOfRun(raw, Spot, a, d.ToDateTime(new TimeOnly(18, 0)))!.CostKr;
            best += PriceModel.DayRange(raw, Spot, a, d).Cheapest!.CostKr;
        }
        hs.UsualKr.Should().BeApproximately(usual, 0.05);
        hs.CheapestKr.Should().BeApproximately(best, 0.05);
    }
}

[TestFixture]
public class PushPlanTests
{
    [Test]
    public void A_phone_without_a_kundenummer_is_asked_once_and_only_in_the_daytime()
    {
        var day = new DateTime(2026, 10, 22, 12, 0, 0);
        PushPlan.ShouldAskForCode(new PushSubscriber(), day).Should().BeTrue();
        PushPlan.ShouldAskForCode(new PushSubscriber(), day.AddHours(10)).Should().BeFalse();   // 22:00
        PushPlan.ShouldAskForCode(new PushSubscriber { AskedForCodeAt = day }, day).Should().BeFalse();
        PushPlan.ShouldAskForCode(new PushSubscriber { CustomerCode = "48217730" }, day).Should().BeFalse();
    }

    [Test]
    public void A_message_is_due_in_the_one_tick_55_to_60_minutes_before()
    {
        var start = new DateTime(2026, 10, 21, 14, 0, 0);
        PushPlan.Due(start, start.AddMinutes(-58)).Should().BeTrue();
        PushPlan.Due(start, start.AddMinutes(-60)).Should().BeTrue();
        PushPlan.Due(start, start.AddMinutes(-62)).Should().BeFalse();
        PushPlan.Due(start, start.AddMinutes(-55)).Should().BeFalse();
        PushPlan.Due(start, start.AddMinutes(-30)).Should().BeFalse();
    }

    [Test]
    public void Old_sent_log_entries_are_forgotten()
    {
        var d = new SubscriberData { SentPushes = [$"{Guid.Empty}|cheapest|2026-10-10 02:00", $"{Guid.Empty}|cheapest|2026-10-20 02:00"] };
        d.Prune(new DateTime(2026, 10, 21, 12, 0, 0));
        d.SentPushes.Should().ContainSingle().Which.Should().EndWith("2026-10-20 02:00");
    }

    [Test]
    public async Task Each_phone_gets_a_message_once_per_window()
    {
        var now = new DateTime(2026, 10, 21, 13, 2, 0);
        // Cheap 14:00-16:00 today (night hours never get a message - nobody is woken up).
        var raw = new List<PricePoint>();
        for (var q = 0; q < 96 * 2; q++)
        {
            var t = new DateTime(2026, 10, 21).AddMinutes(15 * q);
            raw.Add(new PricePoint { TimeDk = t, TimeUtc = t.AddHours(-2), PriceKrPerKwh = t.Hour is >= 14 and < 16 && t.Day == 21 ? 0.1 : 1.0 });
        }
        var prices = new Mock<IPriceSource>();
        prices.Setup(p => p.GetPricesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(raw);
        var store = new MemoryStore();
        store.Update(d => d.Subscribers.Add(new PushSubscriber { Endpoint = "e1", CustomerCode = "48217730", Settings = new HouseholdSettings { ShowAllIn = false } }));
        var sender = new Mock<IPushSender>();
        sender.Setup(s => s.SendAsync(It.IsAny<PushSubscriber>(), It.IsAny<PushMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var notify = new NotifyUseCases(prices.Object, store, sender.Object, Mock.Of<IGridTariffSource>());

        (await notify.RunOnceAsync(now)).Should().Be(1);
        (await notify.RunOnceAsync(now.AddMinutes(1))).Should().Be(0);   // same window: never twice
        sender.Verify(s => s.SendAsync(It.IsAny<PushSubscriber>(), It.Is<PushMessage>(m => m.Body.StartsWith("Kl. 14:00")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task A_phone_without_a_kundenummer_gets_the_sign_up_message_once()
    {
        var prices = new Mock<IPriceSource>();
        prices.Setup(p => p.GetPricesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var store = new MemoryStore();
        store.Update(d => d.Subscribers.Add(new PushSubscriber { Endpoint = "old" }));
        var sender = new Mock<IPushSender>();
        sender.Setup(s => s.SendAsync(It.IsAny<PushSubscriber>(), It.IsAny<PushMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var notify = new NotifyUseCases(prices.Object, store, sender.Object, Mock.Of<IGridTariffSource>());

        var noon = new DateTime(2026, 10, 22, 12, 0, 0);
        (await notify.RunOnceAsync(noon)).Should().Be(1);
        (await notify.RunOnceAsync(noon.AddMinutes(5))).Should().Be(0);
        sender.Verify(s => s.SendAsync(It.IsAny<PushSubscriber>(), PushPlan.AskForCode, It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class MemoryStore : ISubscriberStore
    {
        private readonly SubscriberData _d = new();
        public SubscriberData Get() => _d;
        public void Update(Action<SubscriberData> change) => change(_d);
    }
}

[TestFixture]
public class FrontPageUseCaseTests
{
    private sealed class MemoryPrefs : IHouseholdPrefs
    {
        public HouseholdSettings S = new();
        public HouseholdSettings Get() => S;
        public Task<HouseholdSettings> GetAsync() => Task.FromResult(S);
        public Task SaveAsync(HouseholdSettings settings) { S = settings; return Task.CompletedTask; }
    }

    private static readonly Place Aarhus = new("Vej 1, 8000 Aarhus C", "8000", 56.15, 10.2);
    private static readonly GridCompany Konstant = new("konstant_c", "Konstant", "Konstant", "DK1");
    private static readonly GridCompany Radius = new("radius_c", "Radius", "Radius", "DK2");

    private static (FrontPageUseCases Front, MemoryPrefs Prefs, Mock<IGridTariffSource> Grids) Make()
    {
        var grids = new Mock<IGridTariffSource>();
        var prefs = new MemoryPrefs();
        return (new FrontPageUseCases(Mock.Of<IPriceSource>(), prefs, grids.Object), prefs, grids);
    }

    [Test]
    public async Task A_typed_address_is_the_home_and_is_saved()
    {
        var (front, prefs, grids) = Make();
        grids.Setup(g => g.ResolveAsync("Vej 1, 8000")).ReturnsAsync(Aarhus);
        grids.Setup(g => g.FindAtAsync(Aarhus.Lat, Aarhus.Lon)).ReturnsAsync(Konstant);
        (await front.FindAddressAsync(" Vej 1, 8000 ")).Should().BeOfType<HomeAnswer.Saved>();
        (prefs.S.PostalCode, prefs.S.GridName, prefs.S.PriceArea).Should().Be(("8000", "Konstant", "DK1"));
        (await front.FindAddressAsync("80")).Should().BeOfType<HomeAnswer.Problem>();
    }

    [Test]
    public async Task Somewhere_else_asks_before_the_home_is_changed()
    {
        var (front, prefs, grids) = Make();
        prefs.S.GridSupplierId = Konstant.Id;
        var kbh = new Place("København", "2100", 55.7, 12.57);
        grids.Setup(g => g.ReverseAsync(kbh.Lat, kbh.Lon)).ReturnsAsync(kbh);
        grids.Setup(g => g.FindAtAsync(kbh.Lat, kbh.Lon)).ReturnsAsync(Radius);
        (await front.AtLocationAsync(kbh.Lat, kbh.Lon, askingForNew: false)).Should().BeOfType<HomeAnswer.AskSwitch>();
        prefs.S.GridSupplierId.Should().Be(Konstant.Id);
        (await front.AtLocationAsync(kbh.Lat, kbh.Lon, askingForNew: true)).Should().BeOfType<HomeAnswer.Saved>();
        prefs.S.GridSupplierId.Should().Be(Radius.Id);
    }

    [Test]
    public async Task The_machine_is_kept_within_its_limits()
    {
        var (front, prefs, _) = Make();
        await front.PickMachineAsync(prefs.S, HomeMachine.WashKind);
        await front.SetMachineAsync(prefs.S, 1.5, 99);
        (prefs.S.Machine, prefs.S.Wash.Hours, prefs.S.Wash.Kwh).Should().Be((HomeMachine.WashKind, 1.5, HomeMachine.MaxKwh));
        await front.ResetMachineAsync(prefs.S);
        prefs.S.Wash.Kwh.Should().Be(1.0);
    }

    [Test]
    public async Task A_file_that_is_not_a_bill_says_so()
    {
        var pdf = new Mock<IPdfText>();
        pdf.Setup(p => p.Read(It.IsAny<byte[]>())).Returns("Kære kunde, tak for din ordre.");
        var r = await new BillCheckUseCases(Mock.Of<ISupplierSource>(), pdf.Object).CheckAsync([1, 2, 3]);
        r.Bill.Should().BeNull();
        r.Error.Should().Contain("elregning");

        pdf.Setup(p => p.Read(It.IsAny<byte[]>())).Throws(new InvalidOperationException("not a pdf"));
        (await new BillCheckUseCases(Mock.Of<ISupplierSource>(), pdf.Object).CheckAsync([1])).Error.Should().Contain("PDF");
    }
}

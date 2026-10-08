using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using ITMartinElPriser.Application;
using ITMartinElPriser.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ITMartinElPriser.Tests;

/// <summary>A fake Energinet: today's 96 quarter-hours, cheap at night, dear 17-21. Remembers the areas asked for.</summary>
internal sealed class FakePrices : IPriceSource
{
    public List<string> Areas { get; } = [];

    public static List<PricePoint> Today(DateOnly day) =>
        Enumerable.Range(0, 96).Select(q =>
        {
            var t = day.ToDateTime(TimeOnly.MinValue).AddMinutes(15 * q);
            var h = t.Hour;
            return new PricePoint { TimeDk = t, TimeUtc = t.AddHours(-2), PriceKrPerKwh = h < 6 ? 0.20 : h is >= 17 and < 21 ? 2.00 : 0.80 };
        }).ToList();

    public Task<List<PricePoint>> GetPricesAsync(string priceArea = "DK1", CancellationToken ct = default)
    {
        Areas.Add(priceArea);
        return Task.FromResult(Today(new DateOnly(2026, 10, 8)));
    }

    public Task<List<PricePoint>> GetDayAsync(DateOnly date, string priceArea = "DK1", CancellationToken ct = default) => Task.FromResult(Today(date));
    public Task<List<PricePoint>> GetRangeAsync(DateOnly f, DateOnly t, string priceArea = "DK1", CancellationToken ct = default) => Task.FromResult(Today(f));
}

/// <summary>14:00 Danish time on 8 October 2026 (12:00 UTC).</summary>
internal sealed class Afternoon : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
}

[TestFixture]
public class GetPriceSnapshotTests
{
    [Test]
    public async Task Uses_the_households_area_unless_the_request_says_DK1_or_DK2()
    {
        var prices = new FakePrices();
        var use = new GetPriceSnapshot(prices, new Afternoon());
        await use.ExecuteAsync(new HouseholdSettings { PriceArea = "DK1" }, null, null, null, default);
        await use.ExecuteAsync(new HouseholdSettings { PriceArea = "DK1" }, "DK2", null, null, default);
        await use.ExecuteAsync(new HouseholdSettings { PriceArea = "DK1" }, "Sverige", null, null, default);
        prices.Areas.Should().Equal("DK1", "DK2", "DK1");
    }

    [Test]
    public async Task Now_is_the_quarter_hour_at_14_Danish_time()
    {
        var snap = await new GetPriceSnapshot(new FakePrices(), new Afternoon())
            .ExecuteAsync(new HouseholdSettings { ShowAllIn = false }, null, false, null, default);
        snap.NowKrPerKwh.Should().BeApproximately(0.80, 0.001, "14:00 is a normal-priced hour in the fake day (spot only)");
    }

    [Test]
    public async Task Without_a_list_the_standard_appliances_are_priced() =>
        (await new GetPriceSnapshot(new FakePrices(), new Afternoon()).ExecuteAsync(new HouseholdSettings(), null, null, null, default))
        .Appliances.Should().NotBeEmpty();
}

/// <summary>ElPriser's endpoints with the whole app in memory and a fake Energinet - no network, no push sent.</summary>
[TestFixture]
public class ElPriserApiTests
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private string _dir = null!;

    [SetUp]
    public void Start()
    {
        _dir = Path.Combine(Path.GetTempPath(), "elpriser-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_dir);
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("DataDir", _dir);
            b.ConfigureTestServices(s =>
            {
                s.AddSingleton<IPriceSource>(new FakePrices());
                s.AddSingleton<TimeProvider>(new Afternoon());
                // The scheduler would push to real phones - not in a test.
                foreach (var d in s.Where(d => d.ServiceType == typeof(IHostedService)).ToList()) s.Remove(d);
            });
        });
        _client = _factory.CreateClient();
    }

    [TearDown]
    public void Stop()
    {
        _client.Dispose();
        _factory.Dispose();
        try { Directory.Delete(_dir, true); } catch (IOException) { }
    }

    [Test]
    public async Task Snapshot_is_open_JSON_with_the_price_now_and_the_appliances()
    {
        var r = await _client.GetAsync("/api/snapshot?area=DK1&allIn=true");
        r.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("nowKrPerKwh").GetDouble().Should().BeGreaterThan(0.80, "all-in adds tariffs and taxes to the spot price");
        doc.RootElement.GetProperty("appliances").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Test]
    public async Task The_front_page_needs_no_login()
    {
        var html = await _client.GetStringAsync("/");
        html.Should().Contain("ElPriser").And.NotContain("type=\"password\"");
    }

    [Test]
    public async Task Subscribing_without_an_endpoint_is_refused()
    {
        var r = await _client.PostAsJsonAsync("/api/push/subscribe", new { endpoint = "", p256dh = "x", auth = "y", notifyCheapest = true, notifyExpensive = false });
        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task A_phone_can_subscribe_see_its_status_and_unsubscribe()
    {
        const string ep = "https://push.example/abc";
        (await _client.PostAsJsonAsync("/api/push/subscribe", new { endpoint = ep, p256dh = "k", auth = "a", name = "Test", notifyCheapest = true, notifyExpensive = false }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.GetAsync("/api/push/status?endpoint=" + Uri.EscapeDataString(ep))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.PostAsJsonAsync("/api/push/unsubscribe", new { endpoint = ep })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.GetAsync("/api/push/status?endpoint=" + Uri.EscapeDataString(ep))).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task A_stranger_can_not_read_another_phones_status() =>
        (await _client.GetAsync("/api/push/status?endpoint=" + Uri.EscapeDataString("https://push.example/someone-else")))
        .StatusCode.Should().Be(HttpStatusCode.NotFound);
}

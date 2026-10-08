using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ITMartinTilbud.Application;
using ITMartinTilbud.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinTilbud.Tests;

/// <summary>The whole web app in memory with fake sources: the endpoints, their status codes and the PIN guard.</summary>
public class ApiTests
{
    private const string Pin = "123456";

    private sealed class FakeFoodWaste : IFoodWasteSource
    {
        public bool Enabled { get; set; } = true;
        public bool QuotaSpent { get; set; }

        public Task<List<Clearance>> NearAsync(double lat, double lng, int km, CancellationToken ct) =>
            QuotaSpent ? throw new FoodWasteQuotaException() : Task.FromResult(new List<Clearance>());
    }

    private static (HttpClient Client, FakeOffers Offers, FakeFoodWaste Waste) Start(string? pin = Pin)
    {
        var offers = new FakeOffers();
        var waste = new FakeFoodWaste();
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Tilbud:AdminPin", pin ?? "");
            b.ConfigureTestServices(s =>
            {
                s.AddSingleton<IOfferSource>(offers);
                s.AddSingleton<IFoodWasteSource>(waste);
                s.AddSingleton<IFixedDealStore>(new MemoryDeals());
            });
        });
        return (factory.CreateClient(), offers, waste);
    }

    private static HttpRequestMessage Post(object body, string? pin)
    {
        var r = new HttpRequestMessage(HttpMethod.Post, "/api/faste") { Content = JsonContent.Create(body) };
        if (pin is not null) r.Headers.Add("X-Pin", pin);
        return r;
    }

    [Test]
    public async Task The_page_loads_without_any_login()
    {
        var (c, _, _) = Start();
        var html = await c.GetStringAsync("/");
        html.Should().Contain("Find tilbud").And.NotContain("type=\"password\"");
    }

    [Test]
    public async Task Search_answers_400_with_a_Danish_reason()
    {
        var (c, _, _) = Start();
        var r = await c.GetAsync("/api/tilbud?q=a&lat=56&lng=10");
        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await r.Content.ReadAsStringAsync()).Should().Contain("mindst 2");
    }

    [Test]
    public async Task Search_answers_503_when_the_leaflets_are_down()
    {
        var (c, offers, _) = Start();
        offers.Down = true;
        (await c.GetAsync("/api/tilbud?q=mælk&lat=56&lng=10")).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Test]
    public async Task Search_returns_the_offers()
    {
        var (c, offers, _) = Start();
        offers.ByTerm["kaffe"] = [Make.Offer("Netto", "Kaffe", 40)];
        var list = await c.GetFromJsonAsync<List<Offer>>("/api/tilbud?q=kaffe&lat=56&lng=10");
        list!.Single().Chain.Should().Be("Netto");
    }

    [Test]
    public async Task Fixed_deals_can_be_read_by_anyone_but_changed_only_with_the_PIN()
    {
        var (c, _, _) = Start();
        var deal = new { chain = "Lidl", item = "mælk", days = new[] { 2 } };
        (await c.SendAsync(Post(deal, null))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await c.SendAsync(Post(deal, "forkert"))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await c.SendAsync(Post(deal, Pin))).StatusCode.Should().Be(HttpStatusCode.OK);

        var all = await c.GetFromJsonAsync<List<FixedDeal>>("/api/faste");
        all!.Single().Item.Should().Be("mælk");
        (await c.DeleteAsync($"/api/faste/{all![0].Id}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Without_a_configured_PIN_nobody_can_change_fixed_deals()
    {
        var (c, _, _) = Start(pin: null);
        (await c.SendAsync(Post(new { chain = "Lidl", item = "mælk" }, ""))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task A_fixed_deal_without_an_item_is_400()
    {
        var (c, _, _) = Start();
        (await c.SendAsync(Post(new { chain = "Lidl", item = "" }, Pin))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Food_waste_is_404_when_off_and_429_when_the_day_is_spent()
    {
        var (c, _, waste) = Start();
        waste.Enabled = false;
        (await c.GetAsync("/api/madspild?lat=56&lng=10")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        waste.Enabled = true; waste.QuotaSpent = true;
        (await c.GetAsync("/api/madspild?lat=56&lng=10")).StatusCode.Should().Be((HttpStatusCode)429);
    }
}

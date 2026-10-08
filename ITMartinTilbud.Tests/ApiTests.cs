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
    public async Task The_free_page_has_the_search_and_place_but_not_the_extended_sections()
    {
        var (c, _, _) = Start();
        var html = await c.GetStringAsync("/");
        html.Should().Contain("Find tilbud").And.Contain("Hvor handler du").And.Contain("Godt at vide").And.Contain("udvidet-kort");
        foreach (var paid in new[] { "id=\"mine-kort\"", "id=\"uge-kort\"", "id=\"madspild-kort\"", "id=\"gap-kort\"", "id=\"gem\"", "tb-apps" })
            html.Should().NotContain(paid);
    }

    private static HttpClient NoRedirects(WebApplicationFactory<Program> f) => f.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static WebApplicationFactory<Program> WithCode(string? code) => new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
    {
        b.UseSetting("Tilbud:PaidCode", code ?? "");
        b.ConfigureTestServices(s => { s.AddSingleton<IOfferSource>(new FakeOffers()); s.AddSingleton<IFixedDealStore>(new MemoryDeals()); });
    });

    private static FormUrlEncodedContent Code(string c) => new(new Dictionary<string, string> { ["kode"] = c });

    [Test]
    public async Task A_wrong_code_does_not_unlock()
    {
        using var f = WithCode("rigtig-kode");
        var r = await NoRedirects(f).PostAsync("/udvidet", Code("forkert"));
        r.Headers.Location!.ToString().Should().Contain("kode=forkert");
        r.Headers.Contains("Set-Cookie").Should().BeFalse();
    }

    [Test]
    public async Task The_right_code_unlocks_the_extended_sections()
    {
        using var f = WithCode("rigtig-kode");
        var c = NoRedirects(f);
        var r = await c.PostAsync("/udvidet", Code("rigtig-kode"));
        var cookie = r.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
        cookie.Should().NotContain("rigtig-kode", "the cookie holds a hash, never the code");
        var req = new HttpRequestMessage(HttpMethod.Get, "/");
        req.Headers.Add("Cookie", cookie);
        var html = await (await c.SendAsync(req)).Content.ReadAsStringAsync();
        html.Should().Contain("id=\"mine-kort\"").And.Contain("id=\"gap-kort\"").And.NotContain("udvidet-kort");
    }

    [Test]
    public async Task Without_a_configured_code_nothing_unlocks()
    {
        using var f = WithCode(null);
        var r = await NoRedirects(f).PostAsync("/udvidet", Code(""));
        r.Headers.Contains("Set-Cookie").Should().BeFalse();
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

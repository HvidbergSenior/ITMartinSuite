using ITMartinElPriser.Infrastructure;
using System.Net;
using System.Text;
using FluentAssertions;
using ITMartinElPriser.Core;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace ITMartinElPriser.Tests;

[TestFixture]
public class SupplierCatalogTests
{
    // Trimmed from elpris.dk/data/products_151.json (Konstant Net), 2026-09-28.
    private const string Sample = """
    {"distributionAreaGridArea":"151","distributionAreaName":"Konstant Net A/S","products":[
     {"productId":4200,"productVariantId":0,"name":"NRGi Time ","private1":true,"introOffer":false,
      "supplier":{"name":"NRGi Elhandel A/S - 7011 4500","homepageUrl":"http://nrgi.dk/"},
      "bindingPeriod":"0 mdr.","termination":"Nej","deliveryStart":"3 hverdage/arbejdsdage",
      "orderUrl":"https://nrgi.dk/privat/stroem/","climate":{"sustainableEnergyLevel":0.12},
      "paymentMethods":[{"paymentMethod":1,"name":"Betalingsservice","paymentMethodFee":0.0},{"paymentMethod":-1,"name":"MobilePay"}],
      "productPrices":[{"subscription":23.2,"additionalNordPoolSpot":true,"validFrom":"2025-11-07","validTo":"2099-07-09","lastUpdate":"2025-11-10",
        "matrix":[{"hoursFrom":0,"hoursTo":24,"volumeFrom":0,"volumeTo":999999,"amount":4.23}]}]},
     {"productId":668,"productVariantId":0,"name":"Klima Spot NRGi","private1":false,"introOffer":false,
      "supplier":{"name":"NRGi Elhandel A/S - 7011 4500"},"paymentMethods":[],
      "productPrices":[{"subscription":15,"additionalNordPoolSpot":true,"validFrom":"2025-12-01","validTo":"2099-01-01",
        "matrix":[{"hoursFrom":0,"hoursTo":24,"volumeFrom":0,"volumeTo":999999,"amount":1.0}]}]},
     {"productId":77,"productVariantId":1,"name":"Dag og nat","private1":true,"introOffer":false,
      "supplier":{"name":"Eksempel El A/S"},"paymentMethods":[],
      "productPrices":[{"subscription":0,"additionalNordPoolSpot":true,"validFrom":"2025-01-01","validTo":"2099-01-01",
        "matrix":[{"hoursFrom":0,"hoursTo":6,"volumeFrom":0,"volumeTo":999999,"amount":2.0},
                  {"hoursFrom":6,"hoursTo":24,"volumeFrom":0,"volumeTo":999999,"amount":6.0}]}]}
    ]}
    """;

    private static SupplierCatalog Catalog() =>
        new(new HttpClient(new Fixed(Sample)), NullLogger<SupplierCatalog>.Instance);

    [Test]
    public async Task Maps_an_elpris_dk_product_with_its_price_and_terms()
    {
        var p = (await Catalog().GetAsync("151")).Single(x => x.Name == "NRGi Time");

        p.Company.Should().Be("NRGi Elhandel A/S");          // phone number stripped
        p.Fixed.Should().BeFalse();
        p.SurchargeKrPerKwh.Should().BeApproximately(0.0423, 1e-9);
        p.SubscriptionMonthly.Should().Be(23.2);
        p.FeesMonthly.Should().Be(0);                         // cheapest payment method
        p.PaymentMethods.Should().Be("Betalingsservice, MobilePay");
        p.RenewableShare.Should().Be(0.12);
        p.Link.Should().Be("https://nrgi.dk/privat/stroem/");
        p.MaxKwh.Should().BeNull();                           // 999999 = no limit
        // 4.23 øre x 3,000 kWh + 23.20 kr x 12, incl. VAT
        p.YearlyKr(3000).Should().BeApproximately((0.0423 * 3000 + 23.2 * 12) * 1.25, 0.01);
    }

    [Test]
    public async Task Leaves_out_business_products()
    {
        (await Catalog().GetAsync("151")).Should().NotContain(x => x.Name == "Klima Spot NRGi");
    }

    [Test]
    public async Task Time_of_day_prices_are_weighted_by_hours()
    {
        var p = (await Catalog().GetAsync("151")).Single(x => x.Name == "Dag og nat");
        p.SurchargeKrPerKwh.Should().BeApproximately((2.0 * 6 + 6.0 * 18) / 24 / 100, 1e-9);
    }

    private sealed class Fixed(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
    }
}

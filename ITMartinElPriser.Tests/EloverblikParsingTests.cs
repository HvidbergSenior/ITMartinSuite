using System.Net;
using FluentAssertions;
using ITMartinElPriser.Core;
using ITMartinMitEl.Server.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace ITMartinElPriser.Tests;

[TestFixture]
public class EloverblikParsingTests
{
    // Canned responses shaped like the real customerapi: token exchange, then
    // a time series of two hours starting 2026-09-12T22:00Z (= 13/9 00:00 DK).
    private sealed class Fake : HttpMessageHandler
    {
        public List<string> Urls { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Urls.Add(request.RequestUri!.PathAndQuery);
            var json = request.RequestUri.PathAndQuery.Contains("/token")
                ? """{"result":"ACCESS"}"""
                : """
                  {"result":[{"MyEnergyData_MarketDocument":{"TimeSeries":[{"Period":[
                    {"resolution":"PT1H","timeInterval":{"start":"2026-09-12T22:00:00Z","end":"2026-09-13T00:00:00Z"},
                     "Point":[{"position":"1","out_Quantity.quantity":"0.412","out_Quantity.quality":"A04"},
                              {"position":"2","out_Quantity.quantity":"1.250","out_Quantity.quality":"A04"}]}]}]}}]}
                  """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
        }
    }

    [Test]
    public async Task Hourly_readings_are_converted_to_danish_local_time_and_kwh()
    {
        var fake = new Fake();
        var svc = new EloverblikService(new HttpClient(fake), NullLogger<EloverblikService>.Instance);

        var readings = await svc.GetHourlyAsync("REFRESH", "571313100000000000", new DateOnly(2026, 9, 13), new DateOnly(2026, 9, 13));

        readings.Should().HaveCount(2);
        readings[0].HourDk.Should().Be(new DateTime(2026, 9, 13, 0, 0, 0), "22:00 UTC is 00:00 in Denmark during summer time");
        readings[0].Kwh.Should().Be(0.412);
        readings[1].HourDk.Hour.Should().Be(1);
        readings[1].Kwh.Should().Be(1.25);
        fake.Urls.Should().Contain(u => u.Contains("/gettimeseries/2026-09-13/2026-09-14/Hour"), "dateTo is exclusive");
    }

    [Test]
    public async Task Access_token_is_exchanged_once_and_reused()
    {
        var fake = new Fake();
        var svc = new EloverblikService(new HttpClient(fake), NullLogger<EloverblikService>.Instance);

        await svc.GetHourlyAsync("REFRESH", "x", new DateOnly(2026, 9, 13), new DateOnly(2026, 9, 13));
        await svc.GetHourlyAsync("REFRESH", "x", new DateOnly(2026, 9, 13), new DateOnly(2026, 9, 13));

        fake.Urls.Count(u => u.Contains("/token")).Should().Be(1);
    }
}

[TestFixture]
public class EloverblikNullDocumentTests
{
    private sealed class NullDoc : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var json = request.RequestUri!.PathAndQuery.Contains("/token")
                ? """{"result":"ACCESS"}"""
                : """{"result":[{"MyEnergyData_MarketDocument":null,"success":false,"errorCode":30000},{"MyEnergyData_MarketDocument":{"TimeSeries":[{"Period":null}]}}]}""";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
        }
    }

    [Test]
    public async Task A_day_without_data_yet_is_skipped_not_fatal()
    {
        var svc = new EloverblikService(new HttpClient(new NullDoc()), NullLogger<EloverblikService>.Instance);
        var readings = await svc.GetHourlyAsync("R", "x", new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 13));
        readings.Should().BeEmpty();
    }
}

using FluentAssertions;
using ITMartinTemperatur.Domain;

namespace ITMartinTemperatur.Tests;

public class TemperatureRulesTests
{
    private static readonly GeoPoint Aarhus = new(56.1572, 10.2107);
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 14, 0, 0, TimeSpan.Zero);

    private static Reading R(string station, double lat, double lon, double c, int minutesAgo, bool measured = true) =>
        new(station, new GeoPoint(lat, lon), c, Now.AddMinutes(-minutesAgo), "DMI", measured);

    [Test]
    public void Distance_Aarhus_to_Copenhagen_is_about_157_km() =>
        Aarhus.KmTo(new GeoPoint(55.6761, 12.5683)).Should().BeApproximately(157, 3);

    [Test]
    public void Nearest_fresh_station_inside_the_radius_wins_and_only_its_newest_reading_counts()
    {
        var readings = new[]
        {
            R("Ødum", 56.30, 10.13, 11.0, 70),
            R("Ødum", 56.30, 10.13, 12.5, 10),     // same station, newer
            R("Tirstrup", 56.30, 10.62, 13.0, 5),  // ~29 km - outside 25 km
            R("Gammel", 56.17, 10.20, 20.0, 200),  // close but too old
        };

        var r = TemperatureRules.Decide(readings, null, Aarhus, 25, Now);

        r.Measured.Should().BeTrue();
        r.Main!.Reading.Station.Should().Be("Ødum");
        r.Main.Reading.Celsius.Should().Be(12.5);
        r.Nearby.Should().ContainSingle();
    }

    [Test]
    public void Without_a_station_in_the_radius_the_calculated_value_is_used_and_marked_as_such()
    {
        var model = new Reading("Beregnet for dit sted", Aarhus, 15.6, Now, "Open-Meteo", false);

        var r = TemperatureRules.Decide([R("Tirstrup", 56.30, 10.62, 13.0, 5)], model, Aarhus, 10, Now);

        r.Measured.Should().BeFalse();
        r.Main!.Reading.Measured.Should().BeFalse();
        r.Nearby.Should().BeEmpty();
    }

    [TestCase(0, 25)]
    [TestCase(10, 10)]
    [TestCase(500, 100)]
    public void Radius_is_kept_sensible(int asked, int expected) => TemperatureRules.ClampRadius(asked).Should().Be(expected);

    [Test]
    public void A_box_around_a_point_holds_the_whole_circle()
    {
        var box = Area.Around(Aarhus, 25);
        box.Contains(new GeoPoint(Aarhus.Lat + 0.22, Aarhus.Lon)).Should().BeTrue();   // ~24 km north
        box.Contains(new GeoPoint(Aarhus.Lat, Aarhus.Lon + 0.39)).Should().BeTrue();   // ~24 km east
    }
}

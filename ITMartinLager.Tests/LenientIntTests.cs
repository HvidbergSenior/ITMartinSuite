using System.Text.Json;
using FluentAssertions;
using ITMartinLager.Infrastructure.External;

namespace ITMartinLager.Tests;

// "AI'en kunne ikke svare: ... Path: $.year" (2026-11-24): a year as text or 1973.0 is read, nonsense is empty - never a failed answer.
public class LenientIntTests
{
    private sealed record R(int? Year);
    private static readonly JsonSerializerOptions O = new() { PropertyNameCaseInsensitive = true, Converters = { new LenientIntConverter() } };

    [TestCase("""{"year":1973}""", 1973)]
    [TestCase("""{"year":"1973"}""", 1973)]
    [TestCase("""{"year":"ca. 1975"}""", 1975)]
    [TestCase("""{"year":1973.0}""", 1973)]
    [TestCase("""{"year":"ukendt"}""", null)]
    [TestCase("""{"year":null}""", null)]
    public void Reads_what_can_be_read(string json, int? expected) =>
        JsonSerializer.Deserialize<R>(json, O)!.Year.Should().Be(expected);
}

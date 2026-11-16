using System.Text.Json;
using ITMartinLager.Domain;
using NUnit.Framework;

namespace ITMartinLager.Tests;

// Runs the "one copy" rules over a dump of real finds (FINDS_JSON) and prints the ones they now drop.
public class RealFindsCheck
{
    private sealed record F(int id, string kind, string title, string? artist, string? series, string? number, int? year, string name, decimal? kr);

    [Test, Explicit, Category("Live")]
    public void Real_finds_dropped_now()
    {
        var path = Environment.GetEnvironmentVariable("FINDS_JSON") ?? "";
        var all = JsonSerializer.Deserialize<List<F>>(File.ReadAllText(path))!;
        var dropped = all.Where(f => !Pricing.IsOneCopyOf(new Item { Kind = f.kind, Title = f.title, Artist = f.artist ?? "",
            Series = f.series ?? "", Number = f.number ?? "", Year = f.year }, new Offer(f.name, f.kr, ""))).ToList();
        TestContext.Out.WriteLine($"DROPPED {dropped.Count} of {all.Count}");
        foreach (var f in dropped) TestContext.Out.WriteLine($"DROP | {f.kind} | {f.title} | {f.artist} || {f.name} | {f.kr}");
    }
}

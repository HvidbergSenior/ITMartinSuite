using FluentAssertions;
using ITMartinBibliotek.Server.Services;
using Microsoft.Extensions.Configuration;

namespace ITMartinBibliotek.Tests;

public class CustomerTests
{
    private static Customers Make(params (string key, string pin, string lib)[] list)
    {
        var cfg = new Dictionary<string, string?> { ["Bibliotek:AdminPin"] = "admin123" };
        for (var i = 0; i < list.Length; i++)
        {
            cfg[$"Bibliotek:Kunder:{i}:Key"] = list[i].key;
            cfg[$"Bibliotek:Kunder:{i}:Navn"] = "Jytte";
            cfg[$"Bibliotek:Kunder:{i}:Pin"] = list[i].pin;
            cfg[$"Bibliotek:Kunder:{i}:Bibliotek"] = list[i].lib;
        }
        return new Customers(new ConfigurationBuilder().AddInMemoryCollection(cfg).Build());
    }

    [Test]
    public void Admin_pin_is_the_household_and_customer_pin_is_the_customer()
    {
        var c = Make(("jytte", "jytte42", "Jytte"));
        c.Resolve("admin123")!.IsAdmin.Should().BeTrue();
        c.Resolve("jytte42").Should().Be(new Viewer("jytte", "Jytte"));
        c.Resolve("wrong").Should().BeNull();
        c.Resolve("").Should().BeNull();
    }

    [Test]
    public void A_customer_with_the_admin_pin_or_a_short_pin_is_ignored()
    {
        var c = Make(("a", "admin123", "A"), ("b", "123", "B"));
        c.All.Should().BeEmpty();
        c.Resolve("admin123")!.IsAdmin.Should().BeTrue();
    }

    [Test]
    public void Library_decides_owner_and_unknown_libraries_are_the_household()
    {
        var c = Make(("jytte", "jytte42", "Jytte"));
        c.OwnerForLibrary("jytte").Should().Be("jytte");
        c.OwnerForLibrary("Musik").Should().Be("");
        c.OwnerForLibrary("").Should().Be("");
    }

    private static readonly List<(string, List<string>)> Libs =
    [
        ("Musik", ["/media/Musik"]),
        ("Jytte", ["/media/Musik/Kunder/Jytte"]),
        ("Film", ["/media/Film"]),
    ];

    [TestCase("/media/Musik/Kunder/Jytte/ABBA/Gold", "Jytte")]
    [TestCase("/media/Musik/Kim Larsen/Midt om natten", "Musik")]
    [TestCase("/media/Musik/Kunder/JytteX/Album", "Musik")]
    [TestCase("/media/Film/X-Men/X2.mkv", "Film")]
    [TestCase("/other/place", "")]
    [TestCase(null, "")]
    public void Nested_customer_folder_wins_over_the_household_library(string? path, string expected) =>
        JellyfinClient.LibraryForPath(path, Libs).Should().Be(expected);
}

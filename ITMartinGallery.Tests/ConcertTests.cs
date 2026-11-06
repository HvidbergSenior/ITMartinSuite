using FluentAssertions;
using ITMartinGallery.Domain;

namespace ITMartinGallery.Tests;

// KoncertKlanen: every concert folder is one stage page, newest first, pictures in the order they were taken.
public class ConcertTests
{
    [TestCase("GuldIMund", "Guld I Mund")]
    [TestCase("Northside2027", "Northside 2027")]
    [TestCase("Roskilde_Festival-2028", "Roskilde Festival 2028")]
    public void Folder_names_become_titles(string folder, string title) => ConcertRules.NiceName(folder).Should().Be(title);

    [TestCase("GuldIMund", true)]
    [TestCase("_Galleri", false)]
    [TestCase("Dubletter", false)]
    [TestCase("../etc", false)]
    [TestCase("a/b", false)]
    [TestCase(".hidden", false)]
    public void Only_plain_concert_folders_are_keys(string key, bool ok) => ConcertRules.IsValidKey(key).Should().Be(ok);

    [Test]
    public void Items_follow_the_time_they_were_taken_and_undated_come_last()
    {
        var items = new[]
        {
            new ConcertRules.Item("c.jpg", "img", "/c", null, null),
            new ConcertRules.Item("b.mov", "vid", "/b", null, new DateTime(2026, 11, 5, 21, 48, 0)),
            new ConcertRules.Item("a.jpg", "img", "/a", null, new DateTime(2026, 11, 5, 21, 47, 0)),
        };
        ConcertRules.InPageOrder(items).Select(i => i.Name).Should().Equal("a.jpg", "b.mov", "c.jpg");
        ConcertRules.ShowDate(null, items).Should().Be(new DateTime(2026, 11, 5));
        ConcertRules.ShowDate("2026-11-04", items).Should().Be(new DateTime(2026, 11, 4));
    }

    [Test]
    public void The_order_list_comes_first_and_the_rest_follows()
    {
        var items = new List<ConcertRules.Item>
        {
            new("a.jpg", "img", "/a", null, null), new("b.jpg", "img", "/b", null, null), new("c.jpg", "img", "/c", null, null),
        };
        ConcertRules.Ordered(items, ["c.jpg", "A.JPG", "missing.jpg"]).Select(i => i.Name).Should().Equal("c.jpg", "a.jpg", "b.jpg");
    }
}

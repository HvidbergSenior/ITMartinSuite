using FluentAssertions;
using ITMartinGallery.Domain;

namespace ITMartinGallery.Tests;

// RootFolders=Billeder/2018,Billeder/2019 - year cards at the top of a view-only gallery (2026-11-05).
public class DeepRootTests
{
    private static Gallery G(string? roots) => new("demo", "Demo", "/x", null, false, false, false, false, false, false, RootFolders: roots);

    [Test]
    public void Paths_into_a_folder_become_their_own_cards_in_the_given_order() =>
        G("Billeder/2019, Billeder/2018").DeepRootFolders.Should().Equal("Billeder/2019", "Billeder/2018");

    [Test]
    public void Plain_top_folders_stay_as_before_and_are_not_deep() =>
        G("Billeder,Videoer").DeepRootFolders.Should().BeEmpty();

    [TestCase("Billeder/../../etc")]
    [TestCase("Billeder/./2018")]
    [TestCase("Billeder//2018")]
    public void Climbing_out_of_the_library_is_never_allowed(string bad) => G(bad).DeepRootFolders.Should().BeEmpty();

    [Test]
    public void Backslashes_and_outer_slashes_are_tidied() =>
        G(@"\Billeder\2018\").DeepRootFolders.Should().Equal("Billeder/2018");
}

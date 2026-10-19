using FluentAssertions;
using ITMartin.Media.Application.Services.Steps.DuplicationStep;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace ITMartinFileSorter.Tests.Services;

// User 2026-10-19: "near-duplicates should not be considered duplicate". Annette's burst shots (same capture second)
// and two look-alike photos were dropped as duplicates; only identical files may be grouped now.
public class DuplicateServiceExactOnlyTests
{
    private static MediaFile Photo(string name, string hash, DateTime taken, long size = 1000)
    {
        var f = new MediaFile($"/jobs/test/{name}", taken, MediaType.Image, size, isDateReliable: true);
        f.SetHash(hash);
        return f;
    }

    [Test]
    public async Task Identical_files_are_one_duplicate_group()
    {
        var service = new DuplicateService(NullLogger<DuplicateService>.Instance);
        var t = new DateTime(2026, 5, 16, 17, 53, 3);

        var groups = await service.BuildDuplicateGroupsAsync([Photo("a.jpg", "same", t), Photo("b.jpg", "same", t.AddDays(1))]);

        groups.Should().ContainSingle().Which.Files.Should().HaveCount(2);
    }

    [Test]
    public async Task Burst_shots_in_the_same_second_are_not_duplicates()
    {
        var service = new DuplicateService(NullLogger<DuplicateService>.Instance);
        var t = new DateTime(2026, 5, 16, 17, 53, 3);

        var groups = await service.BuildDuplicateGroupsAsync([Photo("IMG_0250.jpeg", "h1", t), Photo("IMG_0251.jpeg", "h2", t)]);

        groups.Should().BeEmpty();
    }

    [Test]
    public async Task Photos_of_one_facebook_post_with_the_same_time_are_not_duplicates()
    {
        var service = new DuplicateService(NullLogger<DuplicateService>.Instance);
        var posted = new DateTime(2022, 7, 4, 8, 27, 57);

        var groups = await service.BuildDuplicateGroupsAsync(
            [Photo("p1.jpg", "x1", posted), Photo("p2.jpg", "x2", posted), Photo("p3.jpg", "x3", posted)]);

        groups.Should().BeEmpty();
    }
}

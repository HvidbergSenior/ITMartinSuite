using FluentAssertions;
using ITMartin.Ai.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace ITMartinFileSorter.Tests.Services;

// Runs the real bundled model on four real family photos. Slow-ish (a
// second or two) but this is the whole point of the feature - if the model
// stops finding the dog, nothing else about "Fie" matters.
[TestFixture]
public class YoloObjectDetectionServiceTests
{
    private static string Asset(string name) =>
        Path.Combine(AppContext.BaseDirectory, "TestAssets", "objects", name);

    private static YoloObjectDetectionService Create() =>
        new(NullLogger<YoloObjectDetectionService>.Instance);

    [TestCase("dog1.jpg")]
    [TestCase("dog2.jpg")]
    public async Task Finds_the_dog(string file)
    {
        using var svc = Create();
        var found = await svc.DetectAsync(Asset(file));

        found.Select(d => d.Label).Should().Contain("dog", $"{file} has the family dog in the middle of the frame");
        found.First(d => d.Label == "dog").Confidence.Should().BeGreaterThan(0.35);
    }

    [Test]
    public async Task A_cat_is_not_a_dog()
    {
        using var svc = Create();
        var found = await svc.DetectAsync(Asset("cat.jpg"));

        // The model reads this particular fluffy cat as a teddy bear - fine;
        // what matters for "Fie" is that it is never a dog.
        found.Should().NotBeEmpty();
        found.Where(d => d.Label == "dog").Should().BeEmpty();
    }

    [Test]
    public async Task People_are_people()
    {
        using var svc = Create();
        var found = await svc.DetectAsync(Asset("person.jpg"));

        found.Select(d => d.Label).Should().Contain("person");
        found.Where(d => d.Label == "dog").Should().BeEmpty();
    }

    [Test]
    public async Task A_missing_file_is_empty_not_an_error()
    {
        using var svc = Create();
        (await svc.DetectAsync(Asset("nope.jpg"))).Should().BeEmpty();
    }
}

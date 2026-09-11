using System.Text.Json;
using FluentAssertions;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Infrastructure.Persistence;
using ITMartin.Media.Infrastructure.Persistence.Entities;
using ITMartin.Media.Infrastructure.Pipelines.FaceIndex;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ITMartinFileSorter.Tests.Services;

// A face belongs to the ONE person it resembles most. Matching each person
// against the library on their own put the same 91 photos in both Bertil's
// and Eigil's folder on ToshibaTest (2026-09-11): at eleven the two score
// alike, both clear the threshold, and a family library is full of siblings
// and cousins photographed at the same age. These pin the exclusive rule.
[TestFixture]
public class FaceIndexMatchingTests
{
    private static readonly Guid Bertil = Guid.NewGuid();
    private static readonly Guid Eigil = Guid.NewGuid();

    // Embeddings are just directions; three axes are enough to stage
    // "looks mostly like A, somewhat like B".
    private static string Vec(params float[] v) => JsonSerializer.Serialize(v);

    private static FaceIndexService CreateService(out MediaDbContext seed)
    {
        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        seed = new MediaDbContext(options);

        return new FaceIndexService(
            new TestDbContextFactory(options),
            () => Mock.Of<IFaceRecognitionService>(),
            Mock.Of<IThumbnailService>(),
            Mock.Of<ICollectionStore>(),
            Mock.Of<IGpsService>(),
            Mock.Of<IAiEnrichmentService>(),
            NullLogger<FaceIndexService>.Instance);
    }

    private static void SeedPeople(MediaDbContext db)
    {
        db.People.Add(new PersonEntity { Id = Bertil, Name = "Bertil" });
        db.People.Add(new PersonEntity { Id = Eigil, Name = "Eigil" });
        db.PersonReferencePhotos.Add(new PersonReferencePhotoEntity { Id = Guid.NewGuid(), PersonId = Bertil, PhotoPath = "b.jpg", EmbeddingJson = Vec(1, 0, 0) });
        db.PersonReferencePhotos.Add(new PersonReferencePhotoEntity { Id = Guid.NewGuid(), PersonId = Eigil, PhotoPath = "e.jpg", EmbeddingJson = Vec(0, 1, 0) });
    }

    private static void SeedFace(MediaDbContext db, string path, float[] embedding) =>
        db.MediaFaces.Add(new MediaFaceEntity
        {
            Id = Guid.NewGuid(),
            MediaFilePath = path,
            RelativePath = path,
            EmbeddingJson = Vec(embedding),
        });

    [Test]
    public async Task A_face_that_clears_the_threshold_for_both_goes_only_to_the_closer_person()
    {
        var service = CreateService(out var db);
        SeedPeople(db);
        // cos to Bertil = 0.8/1.0 = 0.80, to Eigil = 0.60 - both above 0.45
        SeedFace(db, "/lib/2013/eigil-or-bertil.jpg", [0.8f, 0.6f, 0f]);
        await db.SaveChangesAsync();

        var bertil = await service.FindMatchesAsync(Bertil, 0.45);
        var eigil = await service.FindMatchesAsync(Eigil, 0.45);

        bertil.Select(m => m.MediaFilePath).Should().ContainSingle().Which.Should().Be("/lib/2013/eigil-or-bertil.jpg");
        eigil.Should().BeEmpty("Eigil also cleared the threshold, but Bertil resembles this face more");
    }

    [Test]
    public async Task A_photo_with_both_children_lands_in_both_folders()
    {
        var service = CreateService(out var db);
        SeedPeople(db);
        SeedFace(db, "/lib/2013/both.jpg", [0.95f, 0.1f, 0f]);
        SeedFace(db, "/lib/2013/both.jpg", [0.1f, 0.95f, 0f]);
        await db.SaveChangesAsync();

        (await service.FindMatchesAsync(Bertil)).Should().ContainSingle();
        (await service.FindMatchesAsync(Eigil)).Should().ContainSingle();
    }

    [Test]
    public async Task A_face_below_the_threshold_matches_nobody_even_if_it_is_the_closest()
    {
        var service = CreateService(out var db);
        SeedPeople(db);
        SeedFace(db, "/lib/stranger.jpg", [0.3f, 0.1f, 0.95f]);
        await db.SaveChangesAsync();

        (await service.FindMatchesAsync(Bertil)).Should().BeEmpty();
        (await service.FindMatchesAsync(Eigil)).Should().BeEmpty();
    }

    private sealed class TestDbContextFactory(DbContextOptions<MediaDbContext> options)
        : IDbContextFactory<MediaDbContext>
    {
        public MediaDbContext CreateDbContext() => new(options);
    }
}

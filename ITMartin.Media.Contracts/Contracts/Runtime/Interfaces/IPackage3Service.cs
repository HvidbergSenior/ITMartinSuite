using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

public interface IPackage3Service
{
    /// <summary>
    /// Walks every image under libraryPath, extracting face embeddings for any
    /// file not already indexed. 100% local (FaceONNX) - no API cost. Safe to
    /// re-run - already-indexed files are skipped, so an interrupted run just resumes.
    /// </summary>
    Task IndexFacesAsync(string libraryPath, CancellationToken cancellationToken = default);

    Task<Package3IndexStatus?> GetIndexStatusAsync(string libraryPath, Package3IndexType indexType);

    Task<List<PersonDto>> GetPeopleAsync();

    Task<Guid> AddPersonAsync(string name, IReadOnlyList<ReferencePhotoInput> referencePhotos, string libraryPath);

    Task AddReferencePhotosAsync(Guid personId, IReadOnlyList<ReferencePhotoInput> referencePhotos, string libraryPath);

    Task DeletePersonAsync(Guid personId);

    /// <summary>
    /// Compares the person's reference-photo embeddings against every already-indexed
    /// face in the library and returns ranked matches for review (nothing is saved yet).
    /// </summary>
    Task<List<PersonMatchResult>> FindMatchesAsync(Guid personId, double threshold = 0.45);

    /// <summary>
    /// Records the user's confirmed matches and saves them as a named collection
    /// in collections.json inside libraryPath, so Gallery's Collections view can
    /// read it directly for that specific gallery/library.
    /// </summary>
    Task ConfirmMatchesAsync(Guid personId, IReadOnlyList<string> confirmedFilePaths, string libraryPath);
}

using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

// Final delivery-readiness pass over an already-organized library - removes
// clutter a non-technical recipient shouldn't have to see (empty category
// folders, OS-generated junk files) and hides internal bookkeeping files
// rather than deleting them, since other pipeline steps still read them.
public interface ILibraryPolishService
{
    Task<LibraryPolishResult> PolishAsync(string libraryPath, CancellationToken cancellationToken = default);

    // Content-based rotation fix, separate from the free PolishAsync pass since
    // it makes real Claude API calls - opt-in only, never run automatically.
    // Needed because some already-delivered libraries have photos baked in
    // sideways/upside-down with no original source file left to re-read a
    // correct EXIF Orientation tag from (see ImageConverterService's
    // ApplyOriginalOrientation, which only helps on *future* conversions).
    Task<OrientationFixResult> FixOrientationAsync(string libraryPath, CancellationToken cancellationToken = default);
}

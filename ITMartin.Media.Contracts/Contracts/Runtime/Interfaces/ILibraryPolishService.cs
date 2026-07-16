using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

// Final delivery-readiness pass over an already-organized library - removes
// clutter a non-technical recipient shouldn't have to see (empty category
// folders, OS-generated junk files) and hides internal bookkeeping files
// rather than deleting them, since other pipeline steps still read them.
public interface ILibraryPolishService
{
    Task<LibraryPolishResult> PolishAsync(string libraryPath, CancellationToken cancellationToken = default);
}

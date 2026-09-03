using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

// LargeVideoConvert - the follow-up pass for whatever QuickSort deferred
// (see MediaFile.IsDeferredLargeVideo). Precondition: QuickSort must have
// already run against libraryPath (manifest.json present) - this is what
// tells LargeVideoConvert which files were deferred and where they ended up.
public interface ILargeVideoConvertService
{
    Task<LargeVideoConvertResult> ConvertDeferredVideosAsync(
        string libraryPath,
        Action<int, int, string>? progress = null,
        CancellationToken cancellationToken = default);
}

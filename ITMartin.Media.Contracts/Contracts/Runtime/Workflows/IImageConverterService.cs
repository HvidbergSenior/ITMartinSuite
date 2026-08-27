namespace ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

public interface IImageConverterService
{
    bool NeedsConversion(string path);
    bool ShouldKeepOriginal(string path);
    Task<string?> ConvertToJpgAsync(string inputPath);

    // Cheap, decode-free EXIF tag read - true whenever the source file
    // carries a usable Orientation tag (any value 1-8, including "1 = already
    // upright"), meaning the correct orientation is known without ever
    // needing the expensive face-detection fallback. False only means no
    // tag was present at all - genuinely unknown, not "confirmed wrong".
    bool TryGetSourceOrientation(string path, out ushort orientation);

    // Found 2026-08-27 on mie's real library: some cameras (confirmed so far
    // - Samsung ES60/SL105/ES63, a ~2010 budget point-and-shoot with no
    // orientation sensor) always write Orientation=1 regardless of how the
    // camera was actually held, so TryGetSourceOrientation's "tag present"
    // signal is true but meaningless - the file can still be physically
    // sideways/upside-down with nothing describing the correction. Callers
    // must not treat this camera's files as orientation-known just because a
    // tag exists; route them to the face-detection fallback instead (see
    // FileStatusWorkflowStep's RotationIsCorrect).
    bool IsFromOrientationUnreliableCamera(string path);
}
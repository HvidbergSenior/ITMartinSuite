namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

// Result of applying the rotations a person made in
// SmartFolders/RoterManuelt back onto the real library files.
public sealed class ManualRotationResult
{
    public int Staged { get; set; }
    public int Rotated { get; set; }
    public int AppliedToLibrary { get; set; }
    public List<string> Failed { get; init; } = [];
}

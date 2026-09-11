namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

// Result of replaying rotation-decisions.json onto a library: decisions a
// person (or an earlier paid check) already made about a photo's content,
// applied to every file that still has those exact bytes.
public sealed class CachedRotationResult
{
    // Non-zero decisions that matched a file in the library
    public int Matched { get; set; }
    public int Rotated { get; set; }
    public List<string> Failed { get; init; } = [];
}

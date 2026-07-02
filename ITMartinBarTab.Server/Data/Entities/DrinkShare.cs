namespace ITMartinBarTab.Server.Data.Entities;

public enum ShareType { Full, Half, Taste, None }

public sealed class DrinkShare
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DrinkEntryId { get; set; }
    public Guid ParticipantId { get; set; }
    public ShareType Share { get; set; } = ShareType.None;

    public DrinkEntry DrinkEntry { get; set; } = null!;
    public Participant Participant { get; set; } = null!;

    public static double Weight(ShareType t) => t switch
    {
        ShareType.Full  => 1.0,
        ShareType.Half  => 0.5,
        ShareType.Taste => 0.15,
        _               => 0.0
    };
}

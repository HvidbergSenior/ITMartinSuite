namespace ITMartinMailTriage.Server.Data;

/// <summary>
/// Single-row settings table: the freeform "what matters to me" blurb that
/// drives Claude's relevance judgment, editable from the UI instead of being
/// baked into a prompt.
/// </summary>
public sealed class TriageProfile
{
    public int Id { get; set; } = 1;
    public string UserProfileText { get; set; } =
        "Family, ITMartin business inquiries. Not newsletters, marketing, or automated notices.";
}

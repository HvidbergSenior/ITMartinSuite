namespace ITMartinNotatskriver.Domain;

/// <summary>What a visitor may use. The free version is useful on its own; the extended one is asked for in the chat.
/// (User 2026-10-08: free = text and a few pictures of text; dictation and files are extended.)</summary>
public sealed record UsagePlan(string Name, bool Images, bool Files, bool Dictation, int MaxAttachments)
{
    /// <summary>Text plus up to 3 screenshots or photos of text. Each picture is read by the AI, so they are few.</summary>
    public static readonly UsagePlan Free = new("Gratis", Images: true, Files: false, Dictation: false, MaxAttachments: 3);

    public static readonly UsagePlan Extended = new("Udvidet", Images: true, Files: true, Dictation: true, MaxAttachments: 10);

    /// <summary>Why this kind of attachment can not be added right now - or null when it can.</summary>
    public string? WhyNot(AttachmentKind kind, int alreadyAdded)
    {
        if (kind == AttachmentKind.Image && !Images) return "Billeder er ikke med i denne udgave.";
        if (kind != AttachmentKind.Image && !Files)
            return "Den gratis version tager kun billeder og skærmbilleder – ikke PDF- eller Word-filer.";
        if (alreadyAdded >= MaxAttachments)
            return this == Free
                ? $"Den gratis version tager højst {MaxAttachments} billeder pr. dokument."
                : $"Højst {MaxAttachments} filer/billeder pr. dokument.";
        return null;
    }
}

namespace ITMartinNotatskriver.Domain;

/// <summary>The visitor's work in progress: notes, attachments, the chosen document type and an extra wish.
/// It owns the rules about what may be added and when the document can be written. Lives only in memory.</summary>
public sealed class NoteDraft(UsagePlan plan)
{
    private readonly List<Attachment> _attachments = [];

    public UsagePlan Plan { get; } = plan;
    public string Notes { get; set; } = "";
    public string Extra { get; set; } = "";
    public DocumentType Type { get; set; } = DocumentType.Default;

    /// <summary>The user confirmed that images/PDFs hold no CPR numbers or health data about others.</summary>
    public bool VisualsConfirmed { get; private set; }

    public IReadOnlyList<Attachment> Attachments => _attachments;
    public bool HasVisuals => _attachments.Any(a => a.IsVisual);
    public bool IsFull => _attachments.Count >= Plan.MaxAttachments;

    /// <summary>Everything the user wrote, plus the text of text files - what the personal-data check reads.</summary>
    public string AllText => string.Join("\n", _attachments.Where(a => a.Kind == AttachmentKind.Text).Select(a => a.Text).Append(Notes));

    public bool CanWrite => (Notes.Trim().Length > 0 || _attachments.Count > 0) && (!HasVisuals || VisualsConfirmed);

    /// <summary>Why a file of this kind can not be added now, or null when it can (checked BEFORE the file is read).</summary>
    public string? WhyNotAdd(AttachmentKind kind) => Plan.WhyNot(kind, _attachments.Count);

    public void Add(Attachment attachment)
    {
        if (WhyNotAdd(attachment.Kind) is { } why) throw new NotatskriverException(why);
        _attachments.Add(attachment);
        VisualsConfirmed = false;   // a new picture must be confirmed again
    }

    public void Remove(Attachment attachment) => _attachments.Remove(attachment);

    public void ConfirmVisuals(bool confirmed) => VisualsConfirmed = confirmed;

    public void Clear()
    {
        Notes = ""; Extra = ""; _attachments.Clear(); VisualsConfirmed = false;
    }
}

/// <summary>A problem to show the user, already written in Danish.</summary>
public sealed class NotatskriverException(string message) : Exception(message);

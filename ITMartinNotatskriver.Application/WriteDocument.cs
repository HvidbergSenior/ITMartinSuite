using ITMartinNotatskriver.Domain;

namespace ITMartinNotatskriver.Application;

/// <summary>The result of trying to write a document.</summary>
public abstract record WriteOutcome
{
    public sealed record Written(string Text) : WriteOutcome;

    /// <summary>Stopped before (or by) the AI. Hard = never allowed from here; otherwise the user can remove the
    /// details or let the names be hidden.</summary>
    public sealed record Stopped(bool Hard, string Message, IReadOnlyList<Finding> Findings) : WriteOutcome;

    public sealed record Failed(string Message) : WriteOutcome;
}

/// <summary>Write the document. Order matters: the personal-data check runs BEFORE anything is sent, the daily budget
/// before the AI is called, and the AI's own "BLOKERET" answer is honoured after.</summary>
public sealed class WriteDocument(IDocumentWriter writer, IAiBudget budget, INameHiderFactory hiders)
{
    public const string PatientNotesMessage =
        "Dine noter ligner oplysninger om en patients eller borgers helbred. Den slags må ikke sendes til en AI-tjeneste uden en aftale fra din arbejdsplads – derfor kan det ikke sendes herfra.";
    public const string PersonalDataMessage = "Dine noter indeholder personoplysninger. De bliver ikke sendt, før de er fjernet.";
    public const string BlockedByAiMessage =
        "AI'en fandt CPR-numre eller helbredsoplysninger om en person i dit materiale (måske på et billede) og har ikke skrevet noget dokument.";
    public const string QuotaMessage = "Dagens gratis kvote er brugt op. Prøv igen i morgen – eller skriv til ITMartin@Mensa.dk for mere.";

    /// <param name="hideNames">The user chose "skjul navnene": personal details are replaced before sending.</param>
    public async Task<WriteOutcome> ExecuteAsync(NoteDraft draft, bool hideNames, CancellationToken ct)
    {
        if (!draft.CanWrite) return new WriteOutcome.Failed("Skriv noget, eller tilføj et billede – og bekræft billederne.");

        // Patient notes are never sent - not even with the names hidden.
        var findings = PersonalData.Find(draft.AllText);
        if (PersonalData.LooksLikePatientNotes(findings)) return new WriteOutcome.Stopped(true, PatientNotesMessage, findings);
        if (findings.Count > 0 && !hideNames) return new WriteOutcome.Stopped(false, PersonalDataMessage, findings);

        if (!budget.TryTake(draft.Type.Key)) return new WriteOutcome.Failed(QuotaMessage);

        var hider = hideNames ? hiders.Create() : null;
        var request = new WriteRequest(
            draft.Type,
            hider?.Hide(draft.Notes) ?? draft.Notes,
            hider is null
                ? draft.Attachments
                : draft.Attachments.Select(a => a.Kind == AttachmentKind.Text ? a with { Text = hider.Hide(a.Text) } : a).ToList(),
            draft.Extra);

        try
        {
            var answer = await writer.WriteAsync(request, ct);
            if (WrittenDocument.IsBlocked(answer.Text)) return new WriteOutcome.Stopped(true, BlockedByAiMessage, []);
            var text = hider?.Restore(answer.Text) ?? answer.Text;
            return new WriteOutcome.Written(answer.Truncated ? text + WrittenDocument.TruncatedNote : text);
        }
        catch (NotatskriverException e) { return new WriteOutcome.Failed(e.Message); }
    }
}

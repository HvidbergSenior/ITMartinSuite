using ITMartinNotatskriver.Domain;

namespace ITMartinNotatskriver.Application;

/// <summary>Add an upload to the draft. The plan is checked from the file's name and type BEFORE it is read, so a file
/// the visitor's version can not use is never even opened.</summary>
public sealed class AddAttachment(IAttachmentReader reader)
{
    public const long MaxBytes = 25 * 1024 * 1024;

    /// <returns>Null when added, otherwise the Danish reason it was not.</returns>
    public async Task<string?> ExecuteAsync(NoteDraft draft, string fileName, string contentType, long size, Func<Stream> open, CancellationToken ct)
    {
        if (AttachmentKinds.Detect(fileName, contentType) is not { } kind)
            return $"{fileName}: den filtype kan ikke læses. Brug billede, PDF, Word (.docx) eller tekst.";
        if (draft.WhyNotAdd(kind) is { } why) return why;
        if (size > MaxBytes) return $"{fileName} er for stor (højst 25 MB).";

        try
        {
            await using var stream = open();
            draft.Add(await reader.ReadAsync(fileName, contentType, stream, ct));
            return null;
        }
        catch (NotatskriverException e) { return e.Message; }
        catch (IOException) { return $"{fileName} er for stor (højst 25 MB)."; }
    }
}

using ITMartinNotatskriver.Domain;

namespace ITMartinNotatskriver.Application;

/// <summary>What the document writer (the AI) gets: the type, the notes, the attachments and an extra wish.</summary>
public sealed record WriteRequest(DocumentType Type, string Notes, IReadOnlyList<Attachment> Attachments, string Extra);

/// <summary>The writer's answer. Truncated = the document hit the length limit.</summary>
public sealed record WriterAnswer(string Text, bool Truncated);

/// <summary>Writes the finished document (Claude in production). Throws <see cref="NotatskriverException"/> with a
/// Danish message when the service is busy or fails.</summary>
public interface IDocumentWriter
{
    Task<WriterAnswer> WriteAsync(WriteRequest request, CancellationToken ct);
}

/// <summary>Turns an upload into an <see cref="Attachment"/> (photos upright, shrunk, without GPS data).</summary>
public interface IAttachmentReader
{
    Task<Attachment> ReadAsync(string fileName, string contentType, Stream content, CancellationToken ct);
}

/// <summary>The finished text as a Word document.</summary>
public interface IWordExporter
{
    byte[] Export(string text);
}

/// <summary>A hard daily ceiling on AI calls - the app is open to everyone, so the limit lives in code.</summary>
public interface IAiBudget
{
    /// <returns>False when today's calls are used up.</returns>
    bool TryTake(string what);
}

/// <summary>Replaces names and personal details with placeholders before the AI sees them, and puts them back after.
/// One instance per document, as it remembers what it replaced.</summary>
public interface INameHider
{
    string Hide(string text);
    string Restore(string text);
}

public interface INameHiderFactory
{
    INameHider Create();
}

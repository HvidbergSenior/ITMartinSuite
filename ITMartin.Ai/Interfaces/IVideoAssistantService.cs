namespace ITMartin.Ai.Interfaces;

// Free-form Q&A over one or more video clips, for Vlog Studio's "spørg AI"
// panel - not structured extraction like the other Claude*Service classes,
// so no tool schema, just a plain-text answer. Video itself can't be sent to
// Claude directly, so the caller extracts representative still frames first
// (one per clip is enough for "what changed" / "describe this" questions).
public interface IVideoAssistantService
{
    Task<string> AskAsync(string question, IReadOnlyList<string> frameImagePaths, CancellationToken cancellationToken = default);
}

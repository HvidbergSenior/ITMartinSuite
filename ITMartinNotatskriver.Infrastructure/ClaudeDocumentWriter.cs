using System.Text;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using ITMartinNotatskriver.Application;
using ITMartinNotatskriver.Domain;
using Microsoft.Extensions.Logging;

namespace ITMartinNotatskriver.Infrastructure;

/// <summary>Writes the document with Claude - ONE call per document, however many pages are attached.</summary>
public sealed class ClaudeDocumentWriter(AnthropicClient client, ILogger<ClaudeDocumentWriter> logger) : IDocumentWriter
{
    public const string Model = "claude-opus-5-5";

    internal static readonly string SystemPrompt = $"""
        Du er Notatskriver. Brugeren giver dig sine egne stikord, noter, diktat eller billeder af håndskrevne noter
        og vil have et færdigt dokument på dansk.

        - Læs billeder af håndskrift omhyggeligt. Kan et ord ikke læses, så skriv dit bedste bud efterfulgt af [?].
        - Brug kun indholdet fra noterne. Opfind ikke navne, datoer, tal, aftaler eller fakta. Hvis dokumenttypen normalt
          har en oplysning, der mangler (fx dato eller modtager), så skriv en pladsholder i firkantede parenteser, fx [dato].
        - Bevar navne, tal, beløb og datoer nøjagtigt.
        - Skriv klart og naturligt dansk i hele sætninger. Ingen fyldord.
        - Formatér som almindelig tekst, der kan kopieres direkte ind i Word eller en mail: overskrifter på egen linje,
          punkter med "- ". Ingen markdown-stjerner eller #.
        - Svar kun med selve dokumentet - ingen indledning eller kommentar til brugeren.

        Spærre: Notatskriver må ikke bruges til patient- eller borgeroplysninger. Hvis materialet (også billederne)
        indeholder CPR-numre eller helbredsoplysninger om en bestemt person (fx notater fra en samtale med en patient
        eller borger), så skriv INTET dokument og svar kun med ordet {WrittenDocument.BlockedMarker}.
        """;

    /// <summary>The text part of the message, in the order the AI reads it. Kept apart so it can be tested.</summary>
    internal static string Prompt(WriteRequest r)
    {
        var sb = new StringBuilder().AppendLine($"Lav: {r.Type.Title}. {r.Type.Instruction}");
        if (!string.IsNullOrWhiteSpace(r.Extra)) sb.AppendLine($"Ekstra ønske fra brugeren: {r.Extra.Trim()}");
        if (r.Attachments.Any(a => a.IsVisual)) sb.AppendLine("Billederne/PDF'erne ovenfor er brugerens noter, i rækkefølge.");
        foreach (var a in r.Attachments.Where(a => a.Kind == AttachmentKind.Text))
            sb.AppendLine($"<fil navn=\"{a.Name}\">\n{a.Text}\n</fil>");
        if (!string.IsNullOrWhiteSpace(r.Notes)) sb.AppendLine($"<noter>\n{r.Notes.Trim()}\n</noter>");
        return sb.ToString();
    }

    public async Task<WriterAnswer> WriteAsync(WriteRequest request, CancellationToken ct)
    {
        var content = new List<ContentBlockParam>();
        foreach (var a in request.Attachments)
        {
            if (a.Kind == AttachmentKind.Image)
                content.Add(new ImageBlockParam { Source = new Base64ImageSource { Data = Convert.ToBase64String(a.Data), MediaType = MediaType.ImageJpeg } });
            else if (a.Kind == AttachmentKind.Pdf)
                content.Add(new DocumentBlockParam { Source = new Base64PdfSource { Data = Convert.ToBase64String(a.Data) } });
        }
        content.Add(new TextBlockParam { Text = Prompt(request) });

        try
        {
            var res = await client.Messages.Create(new MessageCreateParams
            {
                Model = Model,
                MaxTokens = 16000,
                OutputConfig = new OutputConfig { Effort = Effort.Medium },
                System = SystemPrompt,
                Messages = [new() { Role = Role.User, Content = content }],
            }, ct);
            logger.LogInformation("Notat {Type}: {In} in / {Out} out tokens, {Count} attachments",
                request.Type.Key, res.Usage.InputTokens, res.Usage.OutputTokens, request.Attachments.Count);
            if (res.StopReason == "refusal") throw new NotatskriverException("AI'en ville ikke skrive dette dokument.");
            var text = string.Concat(res.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text)).Trim();
            return new WriterAnswer(text, res.StopReason == "max_tokens");
        }
        catch (AnthropicRateLimitException) { throw new NotatskriverException("Der er travlt lige nu. Vent et minut og prøv igen."); }
        catch (AnthropicApiException e)
        {
            logger.LogWarning(e, "Claude call failed");
            throw new NotatskriverException("AI-tjenesten svarede ikke som den skulle. Prøv igen om lidt.");
        }
    }
}

using ITMartinHjem.Domain;

namespace ITMartinHjem.Application;

// "Opret dig som interesseret -> a chat is created -> we talk about my help" (user 2026-11-23, on the service pages:
// "too much text. i want people to contact me"). One form does both: the person lands in Kunder as interested (kundenummer by mail,
// like Skriv mig op) and gets their own Snak room with the first message, so the talk about the help starts right away.
public sealed class InterestedChatUseCases(Customers customers, SnakStartUseCases snak)
{
    /// <summary>Link = their new room. Code = their kundenummer ("" when Kunder gave none). Error = a Danish line for the visitor.</summary>
    public sealed record Result(string? Link, string Code, string? Error, string Suggestion = "");

    public async Task<Result> StartAsync(string service, string? topic, string? name, string? email, bool consent, string? message,
        string address, CancellationToken ct = default)
    {
        var n = (name ?? "").Trim();
        var text = InterestedChatRules.FirstMessage(service, message);
        // Check the chat part first, so nobody is signed up and then told the message was wrong.
        if (SnakStartRules.Check(n, text) is { } bad) return new(null, "", bad);

        var topics = topic is { Length: > 0 } t ? new[] { t } : [];
        // bogshoppen.dk (user 2026-11-23): a Bogshoppen customer (FollowBogshoppen), not an itmartin.dk trial.
        var note = InterestedChatRules.Note(service, message);
        var signUp = InterestedChatRules.IsBogshoppen(service)
            ? await customers.FollowBogshoppenAsync(n, email, [], note, consent, ct)
            : await customers.RequestTrialAsync(TrialRules.FrontPage, n, email, consent, topics, note, ct);
        if (signUp.Error is not null) return new(null, "", signUp.Error, signUp.Suggestion);

        var room = await snak.StartAsync(n, text, address, ct);
        return room.Link is { } link
            ? new(link, signUp.Code, null)
            : new(null, signUp.Code, InterestedChatRules.SignedUpButNoRoom(signUp.Code, room.Error));
    }
}

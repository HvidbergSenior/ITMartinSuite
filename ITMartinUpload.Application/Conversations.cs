using ITMartinUpload.Domain;

namespace ITMartinUpload.Application;

/// <summary>
/// The chat between Martin and each customer. A message goes into the customer's thread and a push goes to the other
/// side: Martin when a customer writes, the customer when Martin answers.
/// </summary>
public sealed class Conversations(CustomerBook customers, IMessageStore store, IPushSender push)
{
    private readonly object _lock = new();

    public IReadOnlyList<ChatMessage> Read(string slug) =>
        UploadNames.IsValidSlug(slug) ? store.Read(slug) : [];

    /// <summary>Appends a message; false when there is nothing worth storing or the slug is not valid.</summary>
    public bool Add(string slug, string from, string? text)
    {
        if (!UploadNames.IsValidSlug(slug)) return false;
        var clean = Conversation.Clean(text);
        if (clean is null) return false;
        lock (_lock) store.Append(slug, new ChatMessage(from, clean, DateTime.UtcNow));
        return true;
    }

    public bool FromCustomer(string slug, string? text)
    {
        if (!Add(slug, ChatMessage.Customer, text)) return false;
        var name = customers.Find(slug)?.DisplayName ?? slug;
        _ = push.SendAsync(IPushSender.Martin, $"{name} spørger", Conversation.Preview(text!.Trim()), "/svar");
        return true;
    }

    public bool FromMartin(string slug, string? text)
    {
        if (!Add(slug, ChatMessage.Martin, text)) return false;
        _ = push.SendAsync(slug, "Martin har svaret", Conversation.Preview(text!.Trim()), "/upload/" + slug);
        return true;
    }

    public int UnansweredCount(string slug) => Conversation.UnansweredCount(Read(slug));

    public DateTime? LastActivityUtc(string slug) =>
        Read(slug) is { Count: > 0 } all ? all[^1].AtUtc : null;

    /// <summary>The customers, the one who wrote last first (Martin's /svar page).</summary>
    public IEnumerable<UploadCustomer> CustomersByActivity() =>
        customers.Customers.OrderByDescending(c => LastActivityUtc(c.Slug) ?? DateTime.MinValue);

    public string PushKey => push.PublicKey;

    /// <summary>A customer may only subscribe to their own thread, with the token from their link; Martin with the admin pin.</summary>
    public bool Subscribe(string who, string? token, bool martinPinOk, PushRegistration registration)
    {
        var allowed = who == IPushSender.Martin ? martinPinOk : customers.IsAuthorized(who, token);
        if (!allowed) return false;
        push.Subscribe(who, registration with { Who = who });
        return true;
    }
}

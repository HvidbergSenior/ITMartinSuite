using ITMartinHjem.Application;

namespace ITMartinHjem.Server.Services;

// Adapter: Application's IOwnerInbox = the chat in Svar (ChatService is still in the flat part of Hjem).
// Each "prøv gratis" request becomes its own chat: push to Martin, and his reply is mailed to the person.
public sealed class OwnerInbox(ChatService chat) : IOwnerInbox
{
    public Task NewRequestAsync(string name, string email, string text, CancellationToken ct = default) =>
        chat.VisitorWritesAsync("proev-" + Guid.NewGuid().ToString("N"), name, text, email);
}

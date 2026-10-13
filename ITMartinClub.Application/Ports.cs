using ITMartinClub.Domain;

namespace ITMartinClub.Application;

// The ports the use cases need. Implemented in Infrastructure (EF Core + SQLite club.db, web push, Gmail SMTP, photo files).
// Every write saves at once; what comes back is detached, so a change is saved with UpdateAsync.

/// <summary>Places, vehicles, finds, reminders, ideas, meetings, events, tasks and hovedopgaver - always within one team.</summary>
public interface ITeamItems<T> where T : class, ITeamOwned
{
    Task<List<T>> ListAsync(Guid groupId, CancellationToken ct = default);
    Task<T?> GetAsync(Guid groupId, Guid id, CancellationToken ct = default);
    Task AddAsync(T item, CancellationToken ct = default);
    Task UpdateAsync(T item, CancellationToken ct = default);
    Task UpdateAsync(IEnumerable<T> items, CancellationToken ct = default);
    Task DeleteAsync(Guid groupId, Guid id, CancellationToken ct = default);
}

public interface ITeamStore
{
    Task<Group?> GroupAsync(string slug, CancellationToken ct = default);
    Task<Group?> GroupAsync(Guid id, CancellationToken ct = default);
    Task AddGroupAsync(Group group, CancellationToken ct = default);

    /// <summary>Ordered by name.</summary>
    Task<List<Member>> MembersAsync(Guid groupId, CancellationToken ct = default);
    Task<Member?> MemberAsync(Guid groupId, Guid memberId, CancellationToken ct = default);
    Task<Member?> MemberByNameAsync(Guid groupId, string name, CancellationToken ct = default);
    /// <summary>With its group.</summary>
    Task<Member?> MemberByLoginTokenAsync(string token, CancellationToken ct = default);
    /// <summary>The first member of the team with this role (by name) - with its group.</summary>
    Task<Member?> FirstWithRoleAsync(string slug, string role, CancellationToken ct = default);
    Task AddMemberAsync(Member member, CancellationToken ct = default);
    Task UpdateMemberAsync(Member member, CancellationToken ct = default);
    /// <summary>With the member's sessions and posts; tasks keep the name as text.</summary>
    Task RemoveMemberAsync(Guid groupId, Guid memberId, CancellationToken ct = default);

    /// <summary>With Member and Member.Group.</summary>
    Task<List<MemberSession>> SessionsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
    Task AddSessionAsync(MemberSession session, CancellationToken ct = default);
    Task SetSessionExpiryAsync(Guid sessionId, DateTime expiresAt, CancellationToken ct = default);
}

public interface IMessageStore
{
    /// <summary>With its receipts.</summary>
    Task AddAsync(ClubMessage message, CancellationToken ct = default);
    /// <summary>With receipts.</summary>
    Task<ClubMessage?> GetAsync(Guid groupId, Guid id, CancellationToken ct = default);
    /// <summary>Sent by or to the member, newest first, with receipts.</summary>
    Task<List<ClubMessage>> ForMemberAsync(Guid groupId, Guid memberId, int max, CancellationToken ct = default);
    /// <summary>With its message.</summary>
    Task<MessageReceipt?> ReceiptByTokenAsync(string token, CancellationToken ct = default);
    Task UpdateReceiptsAsync(IEnumerable<MessageReceipt> receipts, CancellationToken ct = default);
    /// <summary>Unconfirmed, never reminded receipts of messages that want a confirmation, sent between from and to - with message.</summary>
    Task<List<MessageReceipt>> AwaitingReminderAsync(DateTime sentFrom, DateTime sentTo, CancellationToken ct = default);
}

public interface ITaskStore
{
    /// <summary>Completed tasks under these hovedopgaver.</summary>
    Task<List<Assignment>> CompletedUnderAsync(Guid groupId, IReadOnlyCollection<Guid> mainTaskIds, CancellationToken ct = default);
    Task<int> DeleteCompletedAsync(Guid groupId, CancellationToken ct = default);
    /// <summary>One save: hovedopgaver added/changed/removed and the tasks moved between them. Tasks under a removed one lose it.</summary>
    Task SaveLayoutAsync(Guid groupId, IEnumerable<MainTask> added, IEnumerable<MainTask> changed, IEnumerable<Guid> removed,
        IEnumerable<Assignment> movedTasks, CancellationToken ct = default);
}

public interface ICalendarStore
{
    Task<List<EventRsvp>> RsvpsAsync(IReadOnlyCollection<Guid> eventIds, CancellationToken ct = default);
    Task<List<EventPrep>> PrepsAsync(IReadOnlyCollection<Guid> eventIds, CancellationToken ct = default);
    /// <summary>Not resolved and not expired.</summary>
    Task<List<EventTimeSuggestion>> OpenSuggestionsAsync(IReadOnlyCollection<Guid> eventIds, DateTime nowUtc, CancellationToken ct = default);
    Task<List<EventTimeVote>> VotesAsync(IReadOnlyCollection<Guid> suggestionIds, CancellationToken ct = default);
    Task SetRsvpAsync(Guid eventId, string memberName, string status, DateTime nowUtc, CancellationToken ct = default);
    Task AddPrepAsync(EventPrep prep, CancellationToken ct = default);
    Task AddSuggestionAsync(EventTimeSuggestion suggestion, CancellationToken ct = default);
    Task ResolveSuggestionAsync(Guid suggestionId, CancellationToken ct = default);
    Task VoteAsync(Guid suggestionId, string memberName, string status, CancellationToken ct = default);
}

public interface IChatStore
{
    /// <summary>The newest messages, oldest first.</summary>
    Task<List<ClubChatMessage>> LatestAsync(Guid groupId, int max, CancellationToken ct = default);
    Task AddAsync(ClubChatMessage message, CancellationToken ct = default);
}

public interface IBulletinStore
{
    /// <summary>Newest first, with the member.</summary>
    Task<List<BulletinPost>> LatestAsync(Guid groupId, int max, CancellationToken ct = default);
    Task AddAsync(BulletinPost post, CancellationToken ct = default);
    /// <summary>Only the member's own post. False if it was not theirs or is gone.</summary>
    Task<bool> DeleteOwnAsync(Guid postId, Guid memberId, CancellationToken ct = default);
    Task ClearAsync(Guid groupId, CancellationToken ct = default);
}

public interface IPushNotifier
{
    string PublicKey { get; }
    Task SubscribeAsync(ClubPushSubscription subscription, CancellationToken ct = default);
    /// <summary>Everyone on the team, except one (the sender).</summary>
    Task ToTeamAsync(Guid groupId, string? exceptMember, string title, string body);
    /// <summary>Only these members. url: tapping the notification opens it.</summary>
    Task ToMembersAsync(Guid groupId, IEnumerable<string> memberNames, string title, string body, string? url = null);
}

public interface IMailSender
{
    /// <summary>Without the SMTP password nothing is mailed - the app and push still work and the page says so.</summary>
    bool Configured { get; }
    Task<bool> SendAsync(string to, string fromName, string subject, string body);
}

public interface IPhotoStore
{
    /// <summary>Saves the photo and returns its file name (served at /photo/{name}).</summary>
    Task<string> SaveAsync(byte[] jpeg, CancellationToken ct = default);
}

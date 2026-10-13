using ITMartinClub.Domain;

namespace ITMartinClub.Application;

public sealed class ClubOptions
{
    /// <summary>Demo tier (Club__SeedDemoData on the demo service, never on the real club-web): visitors land in the demo team.</summary>
    public bool DemoMode { get; init; }
    public const string DemoSlug = "demo";
}

/// <summary>The session that logs this browser in. StoreInBrowser: the browser's own copy must be (re)written.</summary>
public sealed record SessionResult(MemberSession Session, bool StoreInBrowser);

public sealed record JoinResult(MemberSession? Session, string? Error);

public enum AdminUnlock { Ok, Wrong, LockedOut }

// Logging in: browser session, server cookie, invitation link (/l/), message link (/m/), join with name + PIN, admin PIN.
public sealed class TeamAccess(ITeamStore teams, AdminPinLimiter adminPins, MemberPinLimiter memberPins, ClubOptions options, TimeProvider clock)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public Task<Group?> GroupAsync(string slug) => teams.GroupAsync(slug);
    public Task<Group?> GroupAsync(Guid id) => teams.GroupAsync(id);

    /// <summary>
    /// stored = the id in browser storage, cookie = the server-set HttpOnly cookie. iOS wipes browser storage after 7 quiet
    /// days - the cookie still knows who this is. A mail link (/m/{token}) logs in by setting that cookie, so a NEWER valid
    /// cookie session for this team wins over what is in browser storage (shared family PC, or storage from another team).
    /// </summary>
    public async Task<SessionResult?> ResolveAsync(string slug, string? stored, string? cookie)
    {
        var storedId = Guid.TryParse(stored, out var s) ? s : (Guid?)null;
        var cookieId = Guid.TryParse(cookie, out var c) ? c : (Guid?)null;
        var ids = new[] { storedId, cookieId }.Where(x => x is not null).Select(x => x!.Value).Distinct().ToList();
        var sessions = ids.Count == 0 ? [] : await teams.SessionsAsync(ids);
        var fromStorage = sessions.FirstOrDefault(x => x.Id == storedId);
        var fromCookie = sessions.FirstOrDefault(x => x.Id == cookieId);

        MemberSession? chosen;
        var fromServerCookie = false;
        if (storedId is null && cookieId is not null) { chosen = fromCookie; fromServerCookie = true; }
        else if (fromCookie is not null && cookieId != storedId && fromCookie.IsValidFor(slug, Now)
                 && (fromStorage is null || fromStorage.Member.Group.Slug != slug || fromCookie.CreatedAt > fromStorage.CreatedAt))
        { chosen = fromCookie; fromServerCookie = true; }
        else chosen = fromStorage;

        if (chosen is not null && chosen.IsValidFor(slug, Now))
        {
            if (chosen.Slide(Now)) await teams.SetSessionExpiryAsync(chosen.Id, chosen.ExpiresAt);
            return new SessionResult(chosen, fromServerCookie);
        }

        // Demo tier: a real visitor never goes through /join (no invite code to share, no PIN to remember), so
        // auto-resolve to a fixed seeded member instead. Persisted like a normal session so it survives navigation.
        if (options.DemoMode && slug == ClubOptions.DemoSlug && await teams.FirstWithRoleAsync(slug, "Forælder") is { } demo)
        {
            var session = new MemberSession { MemberId = demo.Id, Member = demo };
            await teams.AddSessionAsync(session);
            return new SessionResult(session, StoreInBrowser: true);
        }
        return null;
    }

    /// <summary>Invitation link /l/{token}: a new session for the member, or null.</summary>
    public async Task<MemberSession?> LoginByLinkAsync(string token)
    {
        if (token.Length < 20) return null;
        var member = await teams.MemberByLoginTokenAsync(token);
        if (member is null) return null;
        var session = new MemberSession { MemberId = member.Id, Member = member };
        await teams.AddSessionAsync(session);
        return session;
    }

    /// <summary>A new session for a member who proved who they are another way (the message link).</summary>
    public async Task<MemberSession> StartSessionAsync(Guid memberId)
    {
        var session = new MemberSession { MemberId = memberId };
        await teams.AddSessionAsync(session);
        return session;
    }

    public async Task<JoinResult> JoinAsync(Group group, string name, string pin, string inviteCode, string contactInfo)
    {
        name = name.Trim(); pin = pin.Trim();
        if (name.Length == 0) return new(null, "Skriv dit navn.");
        if (pin.Length == 0) return new(null, "Skriv din PIN.");

        var member = await teams.MemberByNameAsync(group.Id, name);
        if (member is not null && memberPins.IsLockedOut(member.Id))
            return new(null, "For mange forkerte forsøg for dette navn. Vent 15 minutter, eller bed en admin om at nulstille PIN.");

        // A correct PIN for a known name already proves who they are - the invite code only exists to gate strangers
        // who don't know a name/PIN yet, so returning members shouldn't have to keep re-typing it.
        var isReturningMember = member is not null && !string.IsNullOrEmpty(member.Pin) && SecretHasher.Verify(pin, member.Pin);

        if (!isReturningMember && inviteCode.Trim() != group.InviteCode)
        {
            if (member is not null && !string.IsNullOrEmpty(member.Pin)) memberPins.RegisterFailure(member.Id);
            return new(null, "Forkert invitationskode.");
        }

        var isNew = member is null;
        if (member is null)
            member = new Member { GroupId = group.Id, Name = name, Pin = SecretHasher.Hash(pin) };   // new name - this PIN becomes theirs
        else if (string.IsNullOrEmpty(member.Pin))
            member.Pin = SecretHasher.Hash(pin);   // an admin reset this member's PIN - claim a fresh one now
        else if (!isReturningMember)
        {
            memberPins.RegisterFailure(member.Id);
            return new(null, "Forkert PIN for dette navn. Bed en admin om at nulstille den, hvis du har glemt den.");
        }

        memberPins.RegisterSuccess(member.Id);   // a correct PIN clears earlier wrong guesses

        // Contact info is re-entered (not required) on every login - blank keeps what was there, a new value overwrites it.
        if (!string.IsNullOrWhiteSpace(contactInfo)) member.ContactInfo = contactInfo.Trim();

        if (isNew) await teams.AddMemberAsync(member); else await teams.UpdateMemberAsync(member);
        var session = new MemberSession { MemberId = member.Id };
        await teams.AddSessionAsync(session);
        return new(session, null);
    }

    /// <summary>
    /// InviteCode stays in plaintext - by design it is shown to the admin and shared with people who should join (a room
    /// code, not a password). AdminPin gates real destructive actions, so it is hashed like a real secret.
    /// </summary>
    public async Task<string?> CreateGroupAsync(string slug, string name, string description, string inviteCode, string adminPin)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Skriv et gruppenavn.";
        if (string.IsNullOrWhiteSpace(inviteCode)) return "Skriv en adgangskode til gruppen.";
        if (string.IsNullOrWhiteSpace(adminPin)) return "Skriv din personlige PIN.";
        await teams.AddGroupAsync(new Group
        {
            Slug = slug, Name = name.Trim(), Description = description.Trim(),
            InviteCode = inviteCode.Trim(), AdminPin = SecretHasher.Hash(adminPin.Trim()),
        });
        return null;
    }

    public bool IsAdminLockedOut(Group group) => adminPins.IsLockedOut(group.Id);

    public AdminUnlock UnlockAdmin(Group group, string pin)
    {
        if (adminPins.IsLockedOut(group.Id)) return AdminUnlock.LockedOut;
        if (SecretHasher.Verify(pin, group.AdminPin)) { adminPins.RegisterSuccess(group.Id); return AdminUnlock.Ok; }
        adminPins.RegisterFailure(group.Id);
        return adminPins.IsLockedOut(group.Id) ? AdminUnlock.LockedOut : AdminUnlock.Wrong;
    }
}

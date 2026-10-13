using ITMartinClub.Domain;

namespace ITMartinClub.Application;

// Chat (the /chat page and the floating widget) and the Opslagstavle. Both push to the rest of the team.
public sealed class TeamTalk(IChatStore chat, IBulletinStore board, IPhotoStore photos, IPushNotifier push)
{
    public Task<List<ClubChatMessage>> ChatAsync(Guid groupId, int max) => chat.LatestAsync(groupId, max);

    public async Task<bool> SayAsync(Guid groupId, Member from, string text)
    {
        text = text.Trim();
        if (text.Length == 0) return false;
        await chat.AddAsync(new ClubChatMessage { GroupId = groupId, MemberId = from.Id, SenderName = from.Name, Text = text });
        _ = push.ToTeamAsync(groupId, from.Name, $"💬 {from.Name}", text);
        return true;
    }

    public Task<List<BulletinPost>> PostsAsync(Guid groupId) => board.LatestAsync(groupId, 50);

    public async Task<bool> PostAsync(Guid groupId, Member from, string content, string tag, byte[]? image)
    {
        content = content.Trim();
        if (content.Length == 0 && image is null) return false;
        var imageFileName = image is null ? null : await photos.SaveAsync(image);
        await board.AddAsync(new BulletinPost { GroupId = groupId, MemberId = from.Id, Content = content, Tag = tag, ImageFileName = imageFileName });
        _ = push.ToTeamAsync(groupId, from.Name, $"📌 {from.Name}", content.Length == 0 ? "📷 delte et billede" : content);
        return true;
    }

    public Task<bool> DeleteOwnPostAsync(Guid postId, Guid memberId) => board.DeleteOwnAsync(postId, memberId);

    public Task ClearPostsAsync(Guid groupId) => board.ClearAsync(groupId);
}

// Steder (StorageLocation) and køretøjer - Opbevaring and Adresser. Opbevaring also works for someone who only has the link.
public sealed class Places(ITeamItems<StorageLocation> places, ITeamItems<Vehicle> vehicles)
{
    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    public async Task<List<StorageLocation>> ListAsync(Guid groupId) => (await places.ListAsync(groupId)).OrderBy(l => l.Name).ToList();

    /// <summary>id null = a new place. Null fields keep what is there (Adresser has no size field).</summary>
    public async Task SaveAsync(Guid groupId, Guid? id, string name, string? address, string? size, string? contents, string byName, bool keepSize = false)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        var loc = id is { } x ? await places.GetAsync(groupId, x) : new StorageLocation { GroupId = groupId, CreatedByName = byName };
        if (loc is null) return;
        loc.Name = name.Trim();
        loc.Address = Clean(address);
        if (!keepSize) loc.ApproxSize = Clean(size);
        loc.Contents = Clean(contents);
        if (id is null) await places.AddAsync(loc); else await places.UpdateAsync(loc);
    }

    public Task DeleteAsync(Guid groupId, Guid id) => places.DeleteAsync(groupId, id);

    public async Task<List<Vehicle>> VehiclesAsync(Guid groupId) => (await vehicles.ListAsync(groupId)).OrderBy(v => v.Name).ToList();

    public async Task SaveVehicleAsync(Guid groupId, Guid? id, string name, string? availability, string byName)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        var v = id is { } x ? await vehicles.GetAsync(groupId, x) : new Vehicle { GroupId = groupId, CreatedByName = byName };
        if (v is null) return;
        v.Name = name.Trim();
        v.Availability = Clean(availability);
        if (id is null) await vehicles.AddAsync(v); else await vehicles.UpdateAsync(v);
    }

    public Task DeleteVehicleAsync(Guid groupId, Guid id) => vehicles.DeleteAsync(groupId, id);
}

// Find det (where did I put X), Påmindelser (a personal "don't forget" per date), Idéer.
public sealed class TeamLists(ITeamItems<FoundItem> found, ITeamItems<PersonalReminder> reminders, ITeamItems<Idea> ideas, IPhotoStore photos)
{
    public async Task<List<FoundItem>> FoundAsync(Guid groupId) => (await found.ListAsync(groupId)).OrderByDescending(i => i.StoredAt).ToList();

    public async Task StoreFoundAsync(Guid groupId, string name, string location, string notes, byte[]? photo, string byName) =>
        await found.AddAsync(new FoundItem
        {
            GroupId = groupId, Name = name.Trim(), Location = location.Trim(), Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            PhotoFileName = photo is null ? null : await photos.SaveAsync(photo), StoredByName = byName,
        });

    public Task DeleteFoundAsync(Guid groupId, Guid id) => found.DeleteAsync(groupId, id);

    public Task<List<PersonalReminder>> RemindersAsync(Guid groupId) => reminders.ListAsync(groupId);

    public async Task AddReminderAsync(Guid groupId, string memberName, string text, DateTime dateUtc, byte[]? photo)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        await reminders.AddAsync(new PersonalReminder
        {
            GroupId = groupId, MemberName = memberName, Text = text.Trim(), Date = dateUtc,
            PhotoFileName = photo is null ? null : await photos.SaveAsync(photo),
        });
    }

    public async Task ReminderDoneAsync(Guid groupId, Guid id)
    {
        var r = await reminders.GetAsync(groupId, id);
        if (r is null) return;
        r.Done = true;
        await reminders.UpdateAsync(r);
    }

    public Task DeleteReminderAsync(Guid groupId, Guid id) => reminders.DeleteAsync(groupId, id);

    public async Task<List<Idea>> IdeasAsync(Guid groupId) => (await ideas.ListAsync(groupId)).OrderByDescending(i => i.CreatedAt).ToList();

    public async Task AddIdeaAsync(Guid groupId, string title, string description, string proposedBy)
    {
        if (string.IsNullOrWhiteSpace(title)) return;
        await ideas.AddAsync(new Idea
        {
            GroupId = groupId, Title = title.Trim(), Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            ProposedByName = proposedBy,
        });
    }

    public Task DeleteIdeaAsync(Guid groupId, Guid id) => ideas.DeleteAsync(groupId, id);
}

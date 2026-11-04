using FalsigForening.Domain;

namespace FalsigForening.Application;

/// <summary>Looks up a company in the public CVR register (by 8-digit number or by name). Null = not found or not reachable.</summary>
public interface ICvrLookup
{
    Task<CvrDetails?> LookupAsync(string numberOrName, CancellationToken ct = default);
}

public sealed record SaveResult(bool Saved, List<string> Problems);

/// <summary>The association's details (tjeklisten 2). Only the board's admins may change them.</summary>
public sealed class ForeningProfile(ITeamStore teams, ITeamItems<ForeningProfil> profiles, ICvrLookup cvr, TimeProvider clock)
{
    /// <summary>The saved details - or new, empty ones the first time.</summary>
    public async Task<ForeningProfil> GetAsync(Guid groupId, CancellationToken ct = default) =>
        (await profiles.ListAsync(groupId, ct)).FirstOrDefault() ?? ForeningProfil.NewFor(groupId);

    public Task<CvrDetails?> LookupCvrAsync(string numberOrName, CancellationToken ct = default) =>
        string.IsNullOrWhiteSpace(numberOrName) ? Task.FromResult<CvrDetails?>(null) : cvr.LookupAsync(numberOrName.Trim(), ct);

    /// <summary>Saves the name (on the group) and the details. Nothing is saved while there are problems.</summary>
    public async Task<SaveResult> SaveAsync(Member by, string name, ForeningProfil details, CancellationToken ct = default)
    {
        if (!by.IsAdmin || by.GroupId != details.GroupId) return new(false, ["Kun bestyrelsens administratorer kan rette foreningens oplysninger."]);

        details.Cvr = details.Cvr.Length == 0 ? "" : ForeningRules.CleanCvr(details.Cvr) is { Length: > 0 } clean ? clean : details.Cvr;
        details.Hjemmeside = ForeningRules.CleanWebsite(details.Hjemmeside);
        details.Email = details.Email.Trim();
        details.Kommune = details.Kommune.Trim();
        var problems = ForeningRules.Problems(name, details);
        if (problems.Count > 0) return new(false, problems);

        var group = await teams.GroupAsync(details.GroupId, ct);
        if (group is null) return new(false, ["Foreningen findes ikke længere."]);
        if (group.Name != name.Trim()) { group.Name = name.Trim(); await teams.UpdateGroupAsync(group, ct); }

        details.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        var existing = (await profiles.ListAsync(details.GroupId, ct)).FirstOrDefault();
        if (existing is null) await profiles.AddAsync(details, ct);
        else { details.Id = existing.Id; await profiles.UpdateAsync(details, ct); }
        return new(true, []);
    }
}

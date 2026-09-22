namespace ITMartinMitEl.Server.Data;

/// <summary>
/// One home's data. Phase 1 had exactly one of these in a json file; now every
/// account gets its own, so the app can be handed to customers and not just family.
/// The contents stay json (HouseholdData / consumption hours) - the shape has served
/// the app well and the data per household is small.
/// </summary>
public sealed class Household
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Mit hjem";
    public string DataJson { get; set; } = "";
    public string ConsumptionJson { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public static class UserRoles
{
    /// <summary>Owns one household - the normal user.</summary>
    public const string Owner = "owner";

    /// <summary>Martin, or someone in his company, helping customers save money.</summary>
    public const string Advisor = "advisor";
}

public sealed class UserAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Stored lowercase; it is the login name.</summary>
    public string Email { get; set; } = "";

    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = UserRoles.Owner;

    /// <summary>The household an owner belongs to. Null for an advisor, who reaches households through grants.</summary>
    public Guid? HouseholdId { get; set; }

    public string DisplayName { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginUtc { get; set; }
}

/// <summary>
/// A code the household owner hands to an advisor. It is the customer's consent:
/// no advisor can reach a household without one, and the owner can take it back.
/// </summary>
public sealed class AdvisorAccess
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public string Code { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresUtc { get; set; } = DateTime.UtcNow.AddDays(30);

    /// <summary>Set when an advisor redeems the code; from then on it is a standing grant.</summary>
    public Guid? AdvisorUserId { get; set; }
    public DateTime? RedeemedUtc { get; set; }

    /// <summary>The owner can revoke at any time; the row stays so the history is visible.</summary>
    public DateTime? RevokedUtc { get; set; }

    public bool IsActive(DateTime nowUtc) =>
        RevokedUtc is null && AdvisorUserId is not null && nowUtc < ExpiresUtc;

    public bool IsRedeemable(DateTime nowUtc) =>
        RevokedUtc is null && AdvisorUserId is null && nowUtc < ExpiresUtc;
}

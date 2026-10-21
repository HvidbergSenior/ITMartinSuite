using System.Security.Cryptography;

namespace ITMartinKunder.Domain;

// Kundekartotek for all apps (user 2026-10-10/12/21): people who said yes to being a customer and people who
// bought. Bibliotek is NOT in here (user 2026-10-21). NO ads and NO newsletters, ever - the e-mail is for access and what they asked for.
public sealed class Customer
{
    public const int MaxName = 80, MaxEmail = 120, MaxInterests = 2000;

    public int Id { get; set; }
    public string Email { get; set; } = "";          // lower-case, unique
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Interests { get; set; } = "";      // what they chose/wrote, per app: "Tilbud ; Anders And-blade, Tintin | leder efter ..."
    public string Note { get; set; } = "";           // Martin's own note
    public string Code { get; set; } = "";           // kundekode: ONE code for all free apps (user 2026-10-21)
    public string UnsubscribeToken { get; set; } = NewToken();
    public DateTime? UnsubscribedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<Consent> Consents { get; set; } = [];
    public List<CustomerEvent> Events { get; set; } = [];

    public bool Active => UnsubscribedAt is null;
    public bool SaidYesTo(string company) => Active && Consents.Any(c => c.Company == company && c.WithdrawnAt is null);
    public IEnumerable<string> Apps => Events.Select(e => e.App).Where(a => a != "").Distinct();

    public static Customer New(string email, string name, DateTime utcNow) =>
        new() { Email = email, Name = Cut(name.Trim(), MaxName), CreatedAt = utcNow };

    /// <summary>A ticked box: the newest yes per company wins, interests are added, an earlier afmeld is lifted (they said yes again).</summary>
    public void SignUp(SignUpRequest r, DateTime utcNow, Func<string> newCode)
    {
        var at = r.AtUtc ?? utcNow;
        if (r.Name.Trim().Length > 0) Name = Cut(r.Name.Trim(), MaxName);
        var company = Companies.All.Contains(r.Company) ? r.Company : Companies.ITMartin;
        var consent = Consents.FirstOrDefault(c => c.Company == company);
        if (consent is null) Consents.Add(consent = new Consent { Company = company });
        (consent.Text, consent.Source, consent.At, consent.WithdrawnAt) = (r.ConsentText, r.Source, at, null);
        UnsubscribedAt = null;
        AddInterests(r.Interests);
        if (Code == "") Code = newCode();
        Events.Add(CustomerEvent.Of(EventKinds.SignedUp, r.App, "", r.Source, 0, at));
    }

    /// <summary>A paid order. It does NOT make them say yes to anything - they are a customer because they bought.</summary>
    public void Bought(PurchaseRequest r)
    {
        if (Events.Any(e => e.Kind == EventKinds.Bought && e.App == r.App && e.Ref == r.Ref && r.Ref != "")) return;   // same order twice
        if (Name == "" && r.Name.Trim().Length > 0) Name = Cut(r.Name.Trim(), MaxName);
        if (r.Phone.Trim().Length > 0) Phone = Cut(r.Phone.Trim(), 40);
        Events.Add(CustomerEvent.Of(EventKinds.Bought, r.App, r.Ref, r.What, r.Kr, r.AtUtc));
    }

    /// <summary>"Afmeld": every yes is withdrawn. The row stays as proof that they said stop (delete = GDPR erasure, separately).</summary>
    public void Unsubscribe(DateTime utcNow)
    {
        UnsubscribedAt ??= utcNow;
        foreach (var c in Consents) c.WithdrawnAt ??= utcNow;
    }

    /// <summary>Martin's corrections on the list page (the e-mail is checked by the use case: real and not taken).</summary>
    public void ChangeDetails(string name, string email, string phone, string note) =>
        (Name, Email, Phone, Note) = (Cut(name.Trim(), MaxName), email, Cut(phone.Trim(), 40), Cut(note.Trim(), 2000));

    public void AddInterests(string interests)
    {
        var i = interests.Trim();
        if (i.Length == 0 || Interests.Split(" ; ").Contains(i)) return;
        Interests = Cut(Interests.Length == 0 ? i : Interests + " ; " + i, MaxInterests);
    }

    // 8 letters/digits without the ones people misread (0/O, 1/I/l): easy to read aloud and type on a phone.
    private const string CodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    public static string NewCode() => string.Concat(Enumerable.Range(0, 8).Select(_ => CodeChars[RandomNumberGenerator.GetInt32(CodeChars.Length)]));
    public static string NewToken() => Guid.NewGuid().ToString("N");

    internal static string Cut(string s, int max) => s.Length > max ? s[..max] : s;
}

/// <summary>A yes to one company, with the exact words they ticked - the proof (markedsføringsloven §10, GDPR art. 7).</summary>
public sealed class Consent
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string Company { get; set; } = "";
    public string Text { get; set; } = "";
    public string Source { get; set; } = "";
    public DateTime At { get; set; }
    public DateTime? WithdrawnAt { get; set; }
}

public static class EventKinds
{
    public const string SignedUp = "Tilmeldt", Bought = "Køb", Imported = "Flyttet";
}

/// <summary>What happened with a customer in an app: signed up there, bought.</summary>
public sealed class CustomerEvent
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string Kind { get; set; } = "";
    public string App { get; set; } = "";
    public string Ref { get; set; } = "";       // order token ...
    public string What { get; set; } = "";
    public int Kr { get; set; }
    public DateTime At { get; set; }

    public static CustomerEvent Of(string kind, string app, string reference, string what, int kr, DateTime at) => new()
    {
        Kind = kind, App = Customer.Cut(app.Trim(), 60), Ref = Customer.Cut(reference.Trim(), 120), What = Customer.Cut(what.Trim(), 500), Kr = kr, At = at,
    };
}

namespace ITMartinPoll.Domain;

// Kundekartotek (user 2026-10-10/12): people who said yes to being a customer - so it is easy for them to get our offers.
// NO ads and NO newsletters, ever. What they sign up to must be obvious, and leaving must be easy ("make easy way to unsubscribe").
public static class CustomerRules
{
    public const string UnsubscribePage = "/afmeld";

    // What a sign-up means, shown as a list right next to the tick box (bogshoppen.dk poll, app trial requests).
    public static readonly IReadOnlyList<string> SignUpMeans =
    [
        "Du bliver skrevet op som kunde: dit navn og din e-mail i vores kundekartotek.",
        "Vi husker, hvad du interesserer dig for – så det er nemt for dig at få vores tilbud på netop det.",
        "Ingen reklamer og ingen nyhedsbreve. Vi deler aldrig din e-mail med nogen.",
        "Du kan afmelde dig når som helst: skriv din e-mail på siden /afmeld – det tager 10 sekunder.",
    ];

    public static string Normalize(string? email) => (email ?? "").Trim().ToLowerInvariant();
}

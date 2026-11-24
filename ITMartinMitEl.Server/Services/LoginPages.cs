namespace ITMartinMitEl.Server.Services;

/// <summary>
/// The two pages that exist before anyone is signed in. Plain html on purpose:
/// they must render without the Blazor circuit, which needs the login they are asking for.
/// </summary>
public static class LoginPages
{
    public static string SignIn(string? error, string? notice, string email) => Page(
        title: "Log ind",
        body: $"""
        <form method="post" action="/login">
            <div class="logo">⚡</div>
            <h1>MinElpris</h1>
            <p class="sub">Dit forbrug, dine apparater, din regning.</p>
            {Message(error, notice)}
            <label for="email">Mail</label>
            <input id="email" type="email" name="email" value="{Escape(email)}" autocomplete="username" required autofocus>
            <label for="password">Adgangskode</label>
            <input id="password" type="password" name="password" autocomplete="current-password" required>
            <button type="submit">Log ind</button>
            <p class="alt">Ny her? <a href="/opret">Opret en konto</a></p>
            <p class="alt">Bare priserne? <a href="https://elpriser.itmartin.dk">ElPriser</a> er gratis og åben for alle.</p>
            <p class="alt"><a href="/glemt">Glemt adgangskoden?</a></p>
        </form>
        """);

    // "Glemt adgangskoden?" (2026-11-24): the page says what happens and what to do next - the same answer whether or not
    // the mail has an account, so nobody can use it to find out who is a customer.
    public static string Forgot(bool sent, bool mailOff, string email) => Page(
        title: "Glemt adgangskode",
        body: $"""
        <form method="post" action="/glemt">
            <div class="logo">🔑</div>
            <h1>Glemt adgangskoden?</h1>
            <p class="sub">Skriv din mail. Så sender vi et link, hvor du vælger en ny adgangskode.</p>
            {(sent ? Message(null, "Tjek din mail. Har du en konto med den mail, får du et link om et øjeblik. Linket virker i 1 time. Kig også i spam/uønsket post.") : "")}
            {(mailOff ? Message("Vi kan ikke sende mail lige nu. Skriv til ITMartin@Mensa.dk eller ring 31 19 47 30, så hjælper Martin dig.", null) : "")}
            <label for="email">Mail</label>
            <input id="email" type="email" name="email" value="{Escape(email)}" autocomplete="username" required autofocus>
            <button type="submit">Send link</button>
            <p class="alt"><a href="/login">Tilbage til log ind</a></p>
            <p class="alt small">Ingen mail efter 5 minutter? Skriv til <a href="mailto:ITMartin@Mensa.dk">ITMartin@Mensa.dk</a> eller ring 31 19 47 30.</p>
        </form>
        """);

    public static string Reset(string token, bool linkWorks, string? error) => !linkWorks
        ? Page(title: "Linket virker ikke", body: $"""
            <form method="get" action="/glemt">
                <div class="logo">⏰</div>
                <h1>Linket virker ikke længere</h1>
                <p class="sub">Et link til ny adgangskode holder 1 time og kan kun bruges én gang.</p>
                <button type="submit">Bed om et nyt link</button>
                <p class="alt"><a href="/login">Tilbage til log ind</a></p>
            </form>
            """)
        : Page(title: "Ny adgangskode", body: $"""
            <form method="post" action="/nulstil">
                <div class="logo">🔑</div>
                <h1>Vælg ny adgangskode</h1>
                <p class="sub">Skriv den samme adgangskode to gange. Bagefter logger du ind med den.</p>
                {Message(error, null)}
                <input type="hidden" name="t" value="{Escape(token)}">
                <label for="password">Ny adgangskode (mindst {AccountService.MinPasswordLength} tegn)</label>
                <input id="password" type="password" name="password" autocomplete="new-password" required minlength="{AccountService.MinPasswordLength}" autofocus>
                <label for="repeat">Skriv den igen</label>
                <input id="repeat" type="password" name="repeat" autocomplete="new-password" required minlength="{AccountService.MinPasswordLength}">
                <button type="submit">Gem ny adgangskode</button>
            </form>
            """);

    // needsCode: sign-up is by invitation (MitEl:SignupCode set) - user 2026-10-09, MinElpris is the paid app.
    public static string Register(string? error, string email, bool needsCode = false) => Page(
        title: "Opret konto",
        body: $"""
        <form method="post" action="/opret">
            <div class="logo">⚡</div>
            <h1>Opret konto</h1>
            <p class="sub">Din egen side med dit forbrug. Dine tal er kun dine.</p>
            {Message(error, null)}
            {(needsCode ? """
            <label for="code">Dit kundenummer</label>
            <input id="code" name="code" autocomplete="off" inputmode="numeric" placeholder="fx 1234 5678" required>
            <p class="alt small">Prisen på MinElpris aftaler vi sammen – den afhænger af, hvad du vil bruge den til. Intet abonnement. Skriv eller ring til Martin (31 19 47 30), så slår han den til på dit kundenummer. Den gratis ElPriser kan alle bruge: <a href="https://elpriser.itmartin.dk">elpriser.itmartin.dk</a></p>
            """ : "")}
            <label for="name">Dit navn</label>
            <input id="name" name="name" autocomplete="name">
            <label for="email">Mail</label>
            <input id="email" type="email" name="email" value="{Escape(email)}" autocomplete="username" required>
            <label for="password">Adgangskode (mindst {AccountService.MinPasswordLength} tegn)</label>
            <input id="password" type="password" name="password" autocomplete="new-password" required minlength="{AccountService.MinPasswordLength}">
            <label for="home">Hvad skal hjemmet hedde?</label>
            <input id="home" name="home" placeholder="Mit hjem">
            <button type="submit">Opret konto</button>
            <p class="alt">Har du allerede en konto? <a href="/login">Log ind</a></p>
            <p class="alt small">Vi gemmer kun det, du selv taster, og det du henter fra din elmåler. Du kan slette hele kontoen igen under Konto.</p>
        </form>
        """);

    private static string Message(string? error, string? notice) =>
        error is { Length: > 0 } ? $"""<div class="err">{Escape(error)}</div>"""
        : notice is { Length: > 0 } ? $"""<div class="ok">{Escape(notice)}</div>"""
        : "";

    private static string Page(string title, string body) => $$$"""
        <!doctype html>
        <html lang="da"><head>
        <meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
        <title>{{{title}}} – MinElpris</title>
        <style>
        :root{--bg:#121917;--card:#1b2420;--line:rgba(255,255,255,.12);--ink:#ecf2ee;--mut:#9fb0a8;--accent:#4fc394}
        @media(prefers-color-scheme:light){:root{--bg:#f7faf8;--card:#fff;--line:#dfe7e3;--ink:#1d2a25;--mut:#5c6b65;--accent:#1f7a5c}}
        *{box-sizing:border-box}
        body{margin:0;min-height:100vh;display:flex;align-items:center;justify-content:center;padding:24px 16px;
             font:16px/1.5 system-ui,Segoe UI,Roboto,sans-serif;background:var(--bg);color:var(--ink)}
        form{background:var(--card);border:1px solid var(--line);border-radius:18px;padding:28px 26px;width:100%;max-width:380px}
        .logo{font-size:34px;text-align:center}
        h1{font-size:24px;margin:6px 0 4px;text-align:center}
        .sub{color:var(--mut);font-size:14px;text-align:center;margin:0 0 18px}
        label{display:block;font-size:13.5px;font-weight:600;margin:14px 0 5px}
        input{width:100%;padding:12px;border:1px solid var(--line);border-radius:11px;font:inherit;background:var(--bg);color:var(--ink)}
        input:focus{outline:none;border-color:var(--accent)}
        button{width:100%;margin-top:20px;padding:13px;border:0;border-radius:11px;background:var(--accent);
               color:#08211a;font:inherit;font-weight:700;font-size:16px;cursor:pointer}
        .alt{color:var(--mut);font-size:13px;text-align:center;margin:14px 0 0}
        .alt.small{font-size:12px;line-height:1.5}
        a{color:var(--accent)}
        .err,.ok{border-radius:10px;padding:10px 12px;font-size:13.5px;margin-bottom:6px}
        .err{background:rgba(220,80,60,.14);border:1px solid rgba(220,80,60,.5)}
        .ok{background:rgba(79,195,148,.14);border:1px solid rgba(79,195,148,.5)}
        </style></head><body>
        {{{body}}}
        </body></html>
        """;

    private static string Escape(string? text) =>
        System.Net.WebUtility.HtmlEncode(text ?? "");
}

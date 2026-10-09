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
            <p class="alt small">Glemt adgangskoden? Skriv til <a href="mailto:ITMartin@Mensa.dk">ITMartin@Mensa.dk</a>, så nulstiller Martin den.</p>
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
            <label for="code">Kode fra Martin</label>
            <input id="code" name="code" autocomplete="off" required>
            <p class="alt small">MinElpris er kun for inviterede. Har du ingen kode, så ring 31 19 47 30 eller skriv til ITMartin@Mensa.dk. Den gratis ElPriser kan alle bruge: <a href="https://elpriser.itmartin.dk">elpriser.itmartin.dk</a></p>
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

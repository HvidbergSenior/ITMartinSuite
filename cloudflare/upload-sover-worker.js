// Cloudflare Worker on the route upload.itmartin.dk/*  (user 2026-11-04)
// Upload runs on the small NAS and is stopped when nobody uses it. When it is stopped, Cloudflare would show
// a bare 502/530 error. This Worker passes every request straight through; only when Upload does not answer
// does a visitor opening a page get a calm Danish "Upload sover - kontakt Martin" page instead.
// API/file calls keep their real error code, so the app itself is never fooled.

const PHONE = "+4531194730";
const MAIL = "ITMartin@Mensa.dk";

export default {
  async fetch(request) {
    let res;
    try {
      res = await fetch(request);
    } catch {
      res = null;
    }
    const down = res === null || [502, 503, 504, 520, 521, 522, 523, 524, 530].includes(res.status);
    if (!down) return res;

    const wantsPage = request.method === "GET" && (request.headers.get("Accept") || "").includes("text/html");
    if (!wantsPage) return res ?? new Response("Upload sover lige nu", { status: 503 });

    return new Response(PAGE, {
      status: 503,
      headers: { "Content-Type": "text/html; charset=utf-8", "Cache-Control": "no-store", "Retry-After": "300" },
    });
  },
};

const SMS_TEXT = encodeURIComponent("Hej Martin, vil du åbne Upload? Jeg vil gerne lægge billeder op.");

const PAGE = `<!doctype html>
<html lang="da">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<meta name="robots" content="noindex">
<title>Upload sover</title>
<style>
:root{--bg:#fff7f3;--card:#ffffff;--text:#1f1a17;--muted:#5c524c;--accent:#d9442b;--accent-text:#ffffff;--line:#f0d9cf}
@media (prefers-color-scheme:dark){:root{--bg:#17110f;--card:#221a17;--text:#f6eeea;--muted:#cbbdb5;--accent:#ff6a4d;--accent-text:#1a0f0c;--line:#3a2b26}}
*{box-sizing:border-box}
body{margin:0;background:var(--bg);color:var(--text);font:17px/1.55 system-ui,-apple-system,"Segoe UI",Roboto,sans-serif;padding:16px}
.card{max-width:520px;margin:8vh auto 0;background:var(--card);border:1px solid var(--line);border-radius:16px;padding:24px}
.brand{font-weight:700;color:var(--accent);letter-spacing:.02em;margin:0 0 12px}
h1{font-size:1.5rem;margin:0 0 8px}
p{margin:0 0 14px;color:var(--muted)}
.btns{display:flex;flex-direction:column;gap:10px;margin-top:18px}
a.btn{display:block;text-align:center;text-decoration:none;font-weight:600;padding:14px 16px;border-radius:12px;border:2px solid var(--accent);color:var(--accent)}
a.btn.main{background:var(--accent);color:var(--accent-text)}
.small{font-size:.9rem;margin-top:16px}
</style>
</head>
<body>
<main class="card">
  <p class="brand">Kolibri Upload</p>
  <h1>Upload sover lige nu</h1>
  <p>Upload er slukket, fordi den ikke er blevet brugt i et par dage. Det sparer kræfter på serveren.</p>
  <p>Kontakt Martin, så åbner han den med det samme. Dine billeder er i sikkerhed, og dit link virker igen, når den er åben.</p>
  <div class="btns">
    <a class="btn main" href="sms:${PHONE}?body=${SMS_TEXT}">Send en SMS til Martin</a>
    <a class="btn" href="tel:${PHONE}">Ring til Martin – 31 19 47 30</a>
    <a class="btn" href="mailto:${MAIL}?subject=${encodeURIComponent("Åbn Upload")}">Skriv en mail</a>
  </div>
  <p class="small">Når Martin har åbnet den, så prøv dit link igen (opdatér siden).</p>
</main>
</body>
</html>`;

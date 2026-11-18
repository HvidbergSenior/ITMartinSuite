// Easy-Pass: one script for all pages (data-page). Mit pas + Min tur live in this browser only (localStorage);
// the server only sees the needs as a search filter. Every storage call is wrapped - private windows may refuse it.
(function () {
    "use strict";
    var $ = function (id) { return document.getElementById(id); };
    var esc = function (s) { return String(s == null ? "" : s).replace(/[&<>"']/g, function (c) { return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]; }); };
    function load(key) { try { return JSON.parse(localStorage.getItem(key) || "[]"); } catch (e) { return []; } }
    function save(key, v) { try { localStorage.setItem(key, JSON.stringify(v)); } catch (e) { } }
    function api(url, opt) {
        return fetch(url, opt).then(function (r) {
            return r.text().then(function (t) {
                var body = t ? JSON.parse(t) : null;
                if (!r.ok) throw new Error((body && body.fejl) || "Noget gik galt. Prøv igen om lidt.");
                return body;
            });
        });
    }
    var pas = load("ep-pas"), tur = load("ep-tur"), katalog = null;
    function katalogP() { return katalog ? Promise.resolve(katalog) : api("/api/katalog").then(function (k) { katalog = k; return k; }); }
    function navCounts() {
        if ($("nav-pas")) $("nav-pas").textContent = pas.length ? "(" + pas.length + ")" : "";
        if ($("nav-tur")) $("nav-tur").textContent = tur.length ? "(" + tur.length + ")" : "";
    }
    var CLS = { Passer: "ep-passer", TjekSelv: "ep-tjek", PasserIkke: "ep-ikke", 0: "ep-passer", 1: "ep-tjek", 2: "ep-ikke" };
    function badge(pasning, ord) { return '<span class="ep-badge ' + CLS[pasning] + '">' + esc(ord) + "</span>"; }
    function copy(input, btn) {
        btn.addEventListener("click", function () {
            var done = function () { btn.textContent = "Kopieret"; };
            if (input.tagName === "TEXTAREA" && input.classList.contains("ep-sr")) { try { navigator.clipboard.writeText(input.value).then(done, function () { }); } catch (e) { } return; }
            try { navigator.clipboard.writeText(input.value).then(done, function () { input.select(); }); } catch (e) { input.select(); }
        });
    }

    // ?pas=a,b on any page = a pass brought from another phone
    var qs = new URLSearchParams(location.search);
    if (qs.get("pas")) { pas = qs.get("pas").split(",").filter(Boolean); save("ep-pas", pas); }
    navCounts();

    var page = document.querySelector("[data-page]");
    if (!page) return;
    var kind = page.getAttribute("data-page");

    // ---------------- Find steder ----------------
    if (kind === "find") {
        var valgtKat = "", pos = null;
        katalogP().then(function (k) {
            if (pas.length) {
                $("pas-tom").hidden = true; $("pas-linje").hidden = false;
                $("pas-linje").textContent = k.behov.filter(function (b) { return pas.indexOf(b.noegle) >= 0; }).map(function (b) { return b.tekst; }).join(" · ");
            }
            $("kategorier").innerHTML = ['<button type="button" class="ep-chip" aria-pressed="true" data-k="">Alle</button>']
                .concat(k.kategorier.map(function (c) { return '<button type="button" class="ep-chip" aria-pressed="false" data-k="' + esc(c) + '">' + esc(c) + "</button>"; })).join("");
            $("kategorier").addEventListener("click", function (e) {
                var b = e.target.closest("[data-k]"); if (!b) return;
                valgtKat = b.getAttribute("data-k");
                [].forEach.call($("kategorier").children, function (c) { c.setAttribute("aria-pressed", c === b ? "true" : "false"); });
                soeg();
            });
            soeg();
        }).catch(function (e) { $("soeg-info").textContent = e.message; });

        function soeg() {
            var p = new URLSearchParams({ q: $("q").value.trim(), behov: pas.join(","), kunPasser: $("kun-passer").checked ? "true" : "false" });
            if (valgtKat) p.set("kategori", valgtKat);
            if (pos) { p.set("lat", pos.lat); p.set("lng", pos.lng); }
            $("soeg-info").textContent = "Søger …";
            api("/api/steder?" + p).then(function (r) {
                $("soeg-info").textContent = r.antal === 0 ? "Ingen steder fundet. Prøv en anden by eller slå filteret fra."
                    : r.antal + (r.antal === 1 ? " sted" : " steder") + (r.ved ? " inden for 30 km af " + r.ved : "") + (r.antal > r.steder.length ? " – de " + r.steder.length + " nærmeste vises" : "") + (pos || r.ved ? ", nærmeste først." : ".");
                $("resultater").innerHTML = r.steder.map(function (s) {
                    return '<a class="ep-sted" href="/sted/' + encodeURIComponent(s.id) + '"><span class="ep-sted-navn">' + esc(s.navn) + "</span>"
                        + '<span class="k-muted">' + esc(s.kategori) + (s.by ? " · " + esc(s.by) : "") + (s.km != null ? " · " + String(s.km).replace(".", ",") + " km" : "") + "</span>"
                        + (pas.length ? badge(s.pasning, s.ord) : "") + "</a>";
                }).join("");
            }).catch(function (e) { $("soeg-info").textContent = e.message; });
        }
        $("soeg-form").addEventListener("submit", function (e) { e.preventDefault(); soeg(); });
        $("kun-passer").addEventListener("change", soeg);
        $("naer").addEventListener("click", function () {
            if (!navigator.geolocation) { $("soeg-info").textContent = "Din browser kan ikke finde din placering. Skriv en by i stedet."; return; }
            $("soeg-info").textContent = "Finder din placering …";
            navigator.geolocation.getCurrentPosition(function (g) { pos = { lat: g.coords.latitude, lng: g.coords.longitude }; soeg(); },
                function () { $("soeg-info").textContent = "Vi fik ikke lov at se din placering. Skriv en by i stedet."; }, { timeout: 10000 });
        });
    }

    // ---------------- Mit pas ----------------
    if (kind === "pas") {
        katalogP().then(function (k) {
            var grupper = [];
            k.behov.forEach(function (b) { if (grupper.indexOf(b.gruppe) < 0) grupper.push(b.gruppe); });
            $("behov-liste").innerHTML = grupper.map(function (g) {
                return '<fieldset class="ep-gruppe"><legend>' + esc(g) + "</legend>" + k.behov.filter(function (b) { return b.gruppe === g; }).map(function (b) {
                    return '<label class="ep-check"><input type="checkbox" value="' + esc(b.noegle) + '"' + (pas.indexOf(b.noegle) >= 0 ? " checked" : "") + "> " + esc(b.tekst) + "</label>";
                }).join("") + "</fieldset>";
            }).join("");
            $("behov-liste").addEventListener("change", function () {
                pas = [].map.call($("behov-liste").querySelectorAll("input:checked"), function (i) { return i.value; });
                save("ep-pas", pas); navCounts(); link();
                $("pas-gemt").textContent = "Gemt. " + pas.length + (pas.length === 1 ? " kryds." : " kryds.");
            });
            function link() { $("pas-link").value = location.origin + "/pas?pas=" + pas.join(","); }
            link();
            $("vis-stort").addEventListener("click", function () {
                var valgt = k.behov.filter(function (b) { return pas.indexOf(b.noegle) >= 0; });
                $("stort").hidden = false;
                $("stort").innerHTML = valgt.length ? "<p>Jeg har brug for, at I ved:</p><ul>" + valgt.map(function (b) { return "<li>" + esc(b.tekst) + "</li>"; }).join("") + "</ul>"
                    : "<p>Sæt kryds ovenfor først.</p>";
            });
        });
        copy($("pas-link"), $("kopier"));
    }

    // ---------------- Guide: one question per screen ----------------
    if (kind === "guide") {
        var g = { grupper: [], sted: null, pos: null, kat: "" }, trin = 1, hist = [];
        var GRUPPE_ORD = { "Bevægelse": "At gå, trapper eller kørestol", "Energi": "Jeg bliver hurtigt træt", "Syn": "At se", "Hørelse": "At høre",
            "Skjulte handicap": "Larm, mange mennesker eller angst", "Overblik": "At få overblik", "Andet": "Ledsagerkort, hund eller allergi" };
        function vis(n, back) {
            if (!back && trin !== n) hist.push(trin);
            trin = n;
            for (var i = 1; i <= 5; i++) $("g" + i).hidden = i !== n;
            $("g-trin").textContent = n <= 4 ? "Trin " + n + " af 4" : "Dit svar";
            window.scrollTo(0, 0);
            var h = $("g" + n).querySelector("h1"); if (h) { h.setAttribute("tabindex", "-1"); h.focus(); }
        }
        [].forEach.call(document.querySelectorAll(".ep-tilbage"), function (b) {
            b.addEventListener("click", function () { if (hist.length) vis(hist.pop(), true); });
        });
        function tile(val, text, on) { return '<button type="button" class="ep-tile" aria-pressed="' + (on ? "true" : "false") + '" data-v="' + esc(val) + '">' + esc(text) + "</button>"; }
        function toggle(box) {
            box.addEventListener("click", function (e) {
                var b = e.target.closest(".ep-tile"); if (!b) return;
                b.setAttribute("aria-pressed", b.getAttribute("aria-pressed") === "true" ? "false" : "true");
            });
        }
        function valgte(box) { return [].map.call(box.querySelectorAll('.ep-tile[aria-pressed="true"]'), function (b) { return b.getAttribute("data-v"); }); }

        katalogP().then(function (k) {
            var grupper = [];
            k.behov.forEach(function (b) { if (grupper.indexOf(b.gruppe) < 0) grupper.push(b.gruppe); });
            var mineGrupper = k.behov.filter(function (b) { return pas.indexOf(b.noegle) >= 0; }).map(function (b) { return b.gruppe; });
            $("g1-valg").innerHTML = grupper.map(function (gr) { return tile(gr, GRUPPE_ORD[gr] || gr, mineGrupper.indexOf(gr) >= 0); }).join("");
            toggle($("g1-valg"));
            $("g1-videre").addEventListener("click", function () {
                g.grupper = valgte($("g1-valg"));
                if (!g.grupper.length) { pas = []; save("ep-pas", pas); navCounts(); return til3(); }
                $("g2-valg").innerHTML = k.behov.filter(function (b) { return g.grupper.indexOf(b.gruppe) >= 0; })
                    .map(function (b) { return tile(b.noegle, b.tekst, pas.indexOf(b.noegle) >= 0); }).join("");
                vis(2);
            });
            $("g1-intet").addEventListener("click", function () { pas = []; save("ep-pas", pas); navCounts(); til3(); });
            toggle($("g2-valg"));
            $("g2-videre").addEventListener("click", function () {
                pas = valgte($("g2-valg")); save("ep-pas", pas); navCounts(); til3();
            });
            $("g4-valg").innerHTML = tile("", "Det er lige meget", false) + k.kategorier.map(function (c) { return tile(c, c, false); }).join("");
            $("g4-valg").addEventListener("click", function (e) {
                var b = e.target.closest(".ep-tile"); if (!b) return;
                g.kat = b.getAttribute("data-v"); svar();
            });
            function til3() {
                var tekst = k.behov.filter(function (b) { return pas.indexOf(b.noegle) >= 0; }).map(function (b) { return b.tekst; });
                $("g3-pas").textContent = tekst.length ? "Dit pas: " + tekst.join(" · ") : "";
                vis(3);
            }
            // Already have a pass? Start at "Hvor vil du gerne hen?" - the person should not answer the same again.
            if (pas.length) til3(); else vis(1);
        });

        $("g3-naer").addEventListener("click", function () {
            if (!navigator.geolocation) { $("g3-info").textContent = "Din telefon kan ikke finde din placering. Skriv en by i stedet."; return; }
            $("g3-info").textContent = "Finder din placering …";
            navigator.geolocation.getCurrentPosition(function (p) { g.pos = { lat: p.coords.latitude, lng: p.coords.longitude }; g.sted = null; $("g3-info").textContent = ""; vis(4); },
                function () { $("g3-info").textContent = "Vi fik ikke lov at se din placering. Skriv en by i stedet."; }, { timeout: 10000 });
        });
        $("g3-form").addEventListener("submit", function (e) {
            e.preventDefault();
            var v = $("g3-by").value.trim();
            if (!v) { $("g3-info").textContent = "Skriv en by eller et postnummer – eller tryk Nær mig."; return; }
            g.sted = v; g.pos = null; $("g3-info").textContent = ""; vis(4);
        });

        function kort(s) {
            return '<a class="ep-sted ep-sted-big" href="/sted/' + encodeURIComponent(s.id) + '"><span class="ep-sted-navn">' + esc(s.navn) + "</span>"
                + '<span class="k-muted">' + esc(s.kategori) + (s.by ? " · " + esc(s.by) : "") + (s.km != null ? " · " + String(s.km).replace(".", ",") + " km" : "") + "</span>"
                + (pas.length ? badge(s.pasning, s.ord) : "") + '<span class="ep-se">Se stedet og din plan</span></a>';
        }
        function svar() {
            var p = new URLSearchParams({ behov: pas.join(",") });
            if (g.sted) p.set("q", g.sted);
            if (g.pos) { p.set("lat", g.pos.lat); p.set("lng", g.pos.lng); }
            if (g.kat) p.set("kategori", g.kat);
            $("g5-svar").textContent = "Finder steder …"; vis(5);
            api("/api/steder?" + p).then(function (r) {
                var passer = r.steder.filter(function (s) { return s.pasning === "Passer" || !pas.length; });
                var tjek = pas.length ? r.steder.filter(function (s) { return s.pasning === "TjekSelv"; }) : [];
                var ikke = pas.length ? r.steder.filter(function (s) { return s.pasning === "PasserIkke"; }) : [];
                var ved = r.ved ? " nær " + r.ved : "";
                $("g5-svar").textContent = r.antal === 0 ? "Vi fandt ingen steder" + ved + " endnu. Prøv en større by i nærheden, eller tryk Nær mig."
                    : !pas.length ? "Her er " + passer.length + (passer.length === 1 ? " sted" : " steder")
                    : passer.length ? passer.length + (passer.length === 1 ? " sted passer" : " steder passer") + " til dig" + ved
                    : "Ingen steder er tjekket helt endnu – men se dem herunder";
                $("g5-passer").innerHTML = passer.map(kort).join("");
                $("g5-tjek-titel").hidden = $("g5-tjek-tekst").hidden = !tjek.length;
                $("g5-tjek").innerHTML = tjek.map(kort).join("");
                $("g5-ikke-boks").hidden = !ikke.length;
                $("g5-ikke-antal").textContent = ikke.length;
                $("g5-ikke").innerHTML = ikke.map(kort).join("");
            }).catch(function (e) { $("g5-svar").textContent = e.message; });
        }
        $("g5-forfra").addEventListener("click", function () { hist = []; g = { grupper: [], sted: null, pos: null, kat: "" }; vis(1, true); });
    }

    // ---------------- Ét sted = svar + plan ----------------
    if (kind === "sted") {
        var id = page.getAttribute("data-id");
        function sendBesked(body) {
            return api("/api/steder/" + encodeURIComponent(id) + "/besked", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) });
        }
        api("/api/steder/" + encodeURIComponent(id) + "?behov=" + encodeURIComponent(pas.join(","))).then(function (d) {
            var s = d.sted, m = d.match, plan = d.plan, paaTur = tur.indexOf(s.id) >= 0;
            document.title = s.navn + " – Easy-Pass";
            var adr = [s.adresse, [s.postnr, s.by].filter(Boolean).join(" ")].filter(Boolean).join(", ");
            var html = '<h1 class="ep-h1">' + esc(s.navn) + "</h1>"
                + '<p class="k-muted">' + esc(s.kategori) + (adr ? " · " + esc(adr) : "") + "</p>"
                + (s.kilde === "Eksempel" ? '<p class="ep-eksempel">Eksempel – ikke et rigtigt sted. Det viser, hvordan Easy-Pass virker.</p>' : "");
            if (pas.length) html += '<div class="ep-match">' + badge(m.pasning, m.ord) + '<p class="ep-raad">' + esc(plan.raad) + "</p></div>";
            else html += '<p class="ep-match">Svar på <a href="/">fire små spørgsmål</a>, så står der, om stedet passer til dig.</p>';
            if (d.antalGodt > 0) html += '<p class="ep-ok">' + d.antalGodt + (d.antalGodt === 1 ? " har" : " har") + " skrevet, at det passede.</p>";
            if (s.beskrivelse) html += "<p>" + esc(s.beskrivelse) + "</p>";
            html += '<div class="ep-row">'
                + (s.telefon ? '<a class="k-btn k-btn-primary" href="tel:' + esc(s.telefon.replace(/\s/g, "")) + '">Ring: ' + esc(s.telefon) + "</a>" : "")
                + (s.hjemmeside ? '<a class="k-btn k-btn-secondary" href="' + esc(/^https?:/.test(s.hjemmeside) ? s.hjemmeside : "https://" + s.hjemmeside) + '" target="_blank" rel="noopener">Stedets hjemmeside</a>' : "")
                + (s.lat != null ? '<a class="k-btn k-btn-secondary" href="https://www.openstreetmap.org/?mlat=' + s.lat + "&mlon=" + s.lng + "#map=17/" + s.lat + "/" + s.lng + '" target="_blank" rel="noopener">Se på kort</a>' : "")
                + '<button id="tur-knap" class="k-btn k-btn-secondary" type="button">' + (paaTur ? "Fjern fra min tur" : "Læg på min tur") + "</button></div>"
                + '<details class="ep-alt"><summary>Alt om stedet</summary><table class="ep-table"><tbody>'
                + d.faciliteter.map(function (f) { return "<tr><th scope=\"row\">" + esc(f.navn) + '</th><td class="ep-svar-' + (f.svar === "Ja" ? "ja" : f.svar === "Nej" ? "nej" : "ukendt") + '">' + esc(f.svar) + "</td></tr>"; }).join("")
                + "</tbody></table>"
                + (s.sidstTjekket ? '<p class="k-muted">Sidst tjekket ' + new Date(s.sidstTjekket).toLocaleDateString("da-DK") + " · kilde: "
                    + (s.kilde === "OpenStreetMap" ? 'OpenStreetMap © <a href="https://www.openstreetmap.org/copyright" target="_blank" rel="noopener">OpenStreetMap-bidragydere</a>' : esc(s.kilde)) + "</p>" : "")
                + "</details>";
            $("sted-kort").innerHTML = html;
            $("tur-knap").addEventListener("click", function () {
                paaTur = !paaTur;
                tur = paaTur ? tur.concat([s.id]) : tur.filter(function (x) { return x !== s.id; });
                save("ep-tur", tur); navCounts();
                this.textContent = paaTur ? "Fjern fra min tur" : "Læg på min tur";
            });

            // The plan
            $("plan").hidden = false;
            if (plan.spoergOm.length) {
                $("p1-indhold").innerHTML = "<p>Ring eller skriv til stedet, og spørg:</p><ul class=\"ep-tjekliste\">"
                    + plan.spoergOm.map(function (q) { return "<li>" + esc(q) + "</li>"; }).join("") + "</ul>"
                    + (s.telefon ? '<p><a class="k-btn k-btn-primary" href="tel:' + esc(s.telefon.replace(/\s/g, "")) + '">Ring: ' + esc(s.telefon) + "</a></p>" : "")
                    + '<button id="kopier-spm" class="k-btn k-btn-secondary" type="button">Kopiér spørgsmålene</button>'
                    + '<textarea id="spm-tekst" class="ep-sr" aria-hidden="true" tabindex="-1">' + esc("Hej. Jeg vil gerne besøge jer og har et par spørgsmål:\n" + plan.spoergOm.map(function (q) { return "- " + q; }).join("\n")) + "</textarea>";
                copy($("spm-tekst"), $("kopier-spm"));
            } else {
                $("p1-indhold").innerHTML = pas.length ? "<p>Alt det, du har brug for, er tjekket. Du behøver ikke ringe først.</p>"
                    : '<p>Svar på <a href="/">fire små spørgsmål</a>, så laver vi en plan til dig.</p>';
            }
            $("p2-liste").innerHTML = ["Telefonen med Easy-Pass (dit pas er her)"].concat(plan.tagMed)
                .map(function (t) { return "<li>" + esc(t) + "</li>"; }).join("");
        }).catch(function (e) { $("sted-kort").innerHTML = "<p>" + esc(e.message) + '</p><a class="k-btn k-btn-secondary" href="/">Find et sted</a>'; });

        $("vis-pas").addEventListener("click", function () {
            katalogP().then(function (k) {
                var valgt = k.behov.filter(function (b) { return pas.indexOf(b.noegle) >= 0; });
                $("stort-pas-tekst").innerHTML = valgt.length ? "<p>Hej. Jeg har brug for, at I ved:</p><ul>" + valgt.map(function (b) { return "<li>" + esc(b.tekst) + "</li>"; }).join("") + "</ul><p>Tak for hjælpen.</p>"
                    : '<p>Du har ikke udfyldt dit pas endnu.</p><p><a href="/">Svar på fire små spørgsmål</a></p>';
                $("stort-pas").hidden = false; $("luk-pas").focus();
            });
        });
        $("luk-pas").addEventListener("click", function () { $("stort-pas").hidden = true; $("vis-pas").focus(); });
        $("gik-godt").addEventListener("click", function () {
            var b = this; b.disabled = true;
            sendBesked({ godt: true }).then(function () { $("gik-svar").textContent = "Tak! Det hjælper den næste."; }).catch(function (e) { b.disabled = false; $("gik-svar").textContent = e.message; });
        });
        $("gik-skidt").addEventListener("click", function () {
            $("gik-svar").textContent = "Skriv herunder, hvad der ikke passede.";
            $("fejl-tekst").focus();
        });

        function send(form, tekst, mail, svarId, ejer) {
            $(form).addEventListener("submit", function (e) {
                e.preventDefault();
                $(svarId).textContent = "Sender …";
                sendBesked({ tekst: $(tekst).value, mail: $(mail).value, ejer: ejer })
                    .then(function () { $(svarId).textContent = "Tak. Vi har fået det og vender tilbage."; $(tekst).value = ""; })
                    .catch(function (er) { $(svarId).textContent = er.message; });
            });
        }
        send("fejl-form", "fejl-tekst", "fejl-mail", "fejl-svar", false);
        send("ejer-form", "ejer-tekst", "ejer-mail", "ejer-svar", true);
    }

    // ---------------- Min tur ----------------
    if (kind === "tur") {
        var delt = qs.get("steder") ? qs.get("steder").split(",").filter(Boolean) : null;
        var vis = delt || tur;
        if (delt) { $("delt").hidden = false; $("gem-delt").addEventListener("click", function () { tur = delt; save("ep-tur", tur); navCounts(); location.href = "/tur"; }); }
        function tegn() {
            $("tur-tom").hidden = vis.length > 0;
            $("del-kort").hidden = vis.length === 0;
            $("tur-link").value = location.origin + "/tur?steder=" + vis.join(",");
            Promise.all(vis.map(function (x) {
                return api("/api/steder/" + encodeURIComponent(x) + "?behov=" + encodeURIComponent(pas.join(","))).catch(function () { return null; });
            })).then(function (list) {
                $("tur-liste").innerHTML = list.map(function (d, i) {
                    if (!d) return "";
                    return '<div class="ep-sted"><a class="ep-sted-navn" href="/sted/' + encodeURIComponent(d.sted.id) + '">' + (i + 1) + ". " + esc(d.sted.navn) + "</a>"
                        + '<span class="k-muted">' + esc(d.sted.kategori) + (d.sted.by ? " · " + esc(d.sted.by) : "") + "</span>"
                        + (pas.length ? badge(d.match.pasning, d.match.ord) : "")
                        + (delt ? "" : '<button class="k-btn k-btn-secondary ep-fjern" type="button" data-id="' + esc(d.sted.id) + '">Fjern</button>') + "</div>";
                }).join("");
            });
        }
        $("tur-liste").addEventListener("click", function (e) {
            var b = e.target.closest(".ep-fjern"); if (!b) return;
            tur = tur.filter(function (x) { return x !== b.getAttribute("data-id"); }); vis = tur;
            save("ep-tur", tur); navCounts(); tegn();
        });
        copy($("tur-link"), $("kopier-tur"));
        tegn();
    }

    // ---------------- Admin ----------------
    if (kind === "admin") {
        var pin = ""; try { pin = sessionStorage.getItem("ep-pin") || ""; } catch (e) { }
        var adm = function (url, opt) { opt = opt || {}; opt.headers = Object.assign({ "X-Pin": pin }, opt.headers || {}); return api(url, opt); };
        function aaben() {
            adm("/api/admin/tjek", { method: "POST" }).then(function () {
                try { sessionStorage.setItem("ep-pin", pin); } catch (e) { }
                $("pin-kort").hidden = true; $("admin-indhold").hidden = false; start();
            }).catch(function () { if (pin) $("pin-svar").textContent = "Forkert PIN."; });
        }
        $("pin-form").addEventListener("submit", function (e) { e.preventDefault(); pin = $("pin").value; aaben(); });
        if (pin) aaben();

        function beskeder() {
            adm("/api/admin/beskeder").then(function (l) {
                $("beskeder").innerHTML = l.length ? l.map(function (t) {
                    return '<div class="ep-sted' + (t.klaret ? " ep-klaret" : "") + '"><b>' + (t.type === "Ejer" ? "Stedet selv: " : t.type === "Godt" ? "Det passede: " : "Fejl: ") + esc(t.sted) + "</b>"
                        + "<span>" + esc(t.tekst) + "</span><span class=\"k-muted\">" + new Date(t.tid).toLocaleString("da-DK") + (t.mail ? " · " + esc(t.mail) : "") + "</span>"
                        + (t.klaret ? '<span class="k-muted">Klaret</span>' : '<button class="k-btn k-btn-secondary" type="button" data-klaret="' + esc(t.id) + '">Klaret</button>') + "</div>";
                }).join("") : '<p class="k-muted">Ingen beskeder endnu.</p>';
            });
        }
        $("beskeder").addEventListener("click", function (e) {
            var b = e.target.closest("[data-klaret]"); if (!b) return;
            adm("/api/admin/beskeder/" + b.getAttribute("data-klaret") + "/klaret", { method: "POST" }).then(beskeder);
        });

        function osmStatus() { adm("/api/admin/osm").then(function (r) { $("osm-status").textContent = r.status; if (/Henter/.test(r.status)) setTimeout(osmStatus, 5000); }); }
        $("osm-hent").addEventListener("click", function () {
            adm("/api/admin/osm", { method: "POST" }).then(function () { setTimeout(osmStatus, 1000); }).catch(function (e) { $("osm-status").textContent = e.message; });
        });
        function start() {
            osmStatus();
            beskeder();
            katalogP().then(function (k) {
                $("s-kategori").innerHTML = k.kategorier.map(function (c) { return "<option>" + esc(c) + "</option>"; }).join("");
                $("s-fac").innerHTML = k.faciliteter.map(function (f) {
                    return '<label class="ep-fac-row"><span>' + esc(f.navn) + '</span><select data-f="' + esc(f.noegle) + '" class="ep-input"><option value="0">Ikke tjekket</option><option value="1">Ja</option><option value="2">Nej</option></select></label>';
                }).join("");
            });
        }
        function fyld(s) {
            $("s-id").value = s ? s.id : ""; $("s-navn").value = s ? s.navn : ""; $("s-kategori").value = s ? s.kategori : "Andet";
            $("s-adresse").value = s ? s.adresse : ""; $("s-postnr").value = s ? s.postnr : ""; $("s-by").value = s ? s.by : "";
            $("s-lat").value = s && s.lat != null ? s.lat : ""; $("s-lng").value = s && s.lng != null ? s.lng : "";
            $("s-web").value = s ? s.hjemmeside : ""; $("s-tlf").value = s ? s.telefon : ""; $("s-beskrivelse").value = s ? s.beskrivelse : "";
            [].forEach.call($("s-fac").querySelectorAll("select"), function (sel) { sel.value = String(s && s.faciliteter[sel.getAttribute("data-f")] || 0); });
            $("s-slet").hidden = !s; $("sted-svar").textContent = "";
        }
        $("s-ny").addEventListener("click", function () { fyld(null); });
        $("ret-soeg").addEventListener("input", function () {
            var q = this.value.trim(); if (q.length < 2) { $("ret-fund").innerHTML = ""; return; }
            api("/api/steder?q=" + encodeURIComponent(q)).then(function (r) {
                $("ret-fund").innerHTML = r.steder.slice(0, 12).map(function (s) { return '<button type="button" class="ep-chip" data-ret="' + esc(s.id) + '">' + esc(s.navn) + "</button>"; }).join("");
            });
        });
        $("ret-fund").addEventListener("click", function (e) {
            var b = e.target.closest("[data-ret]"); if (!b) return;
            api("/api/steder/" + b.getAttribute("data-ret")).then(function (d) { fyld(d.sted); });
        });
        $("sted-form").addEventListener("submit", function (e) {
            e.preventDefault();
            var num = function (v) { v = v.trim().replace(",", "."); return v === "" ? null : Number(v); };
            var fac = {};
            [].forEach.call($("s-fac").querySelectorAll("select"), function (sel) { if (sel.value !== "0") fac[sel.getAttribute("data-f")] = Number(sel.value); });
            var body = {
                navn: $("s-navn").value, kategori: $("s-kategori").value, adresse: $("s-adresse").value, postnr: $("s-postnr").value, by: $("s-by").value,
                lat: num($("s-lat").value), lng: num($("s-lng").value), hjemmeside: $("s-web").value, telefon: $("s-tlf").value,
                beskrivelse: $("s-beskrivelse").value, faciliteter: fac, kilde: "Easy-Pass",
            };
            if ($("s-id").value) body.id = $("s-id").value;
            adm("/api/admin/sted", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) })
                .then(function (r) { $("s-id").value = r.id; $("s-slet").hidden = false; $("sted-svar").textContent = "Gemt."; })
                .catch(function (er) { $("sted-svar").textContent = er.message; });
        });
        $("s-slet").addEventListener("click", function () {
            var btn = this;
            if (btn.getAttribute("data-sure") !== "1") { btn.setAttribute("data-sure", "1"); btn.textContent = "Tryk igen for at slette"; return; }
            adm("/api/admin/sted/" + encodeURIComponent($("s-id").value), { method: "DELETE" }).then(function () {
                fyld(null); btn.removeAttribute("data-sure"); btn.textContent = "Slet stedet"; $("sted-svar").textContent = "Slettet.";
            });
        });
        $("csv-hent").addEventListener("click", function () {
            var f = $("csv-fil").files[0]; if (!f) { $("csv-svar").textContent = "Vælg en fil først."; return; }
            $("csv-svar").textContent = "Henter …";
            f.text().then(function (t) { return adm("/api/admin/import", { method: "POST", headers: { "Content-Type": "text/csv" }, body: t }); })
                .then(function (r) {
                    $("csv-svar").innerHTML = esc(r.nye + " nye, " + r.opdateret + " opdateret.") + (r.sprunget.length ? "<br>Sprunget over:<br>" + r.sprunget.map(esc).join("<br>") : "");
                }).catch(function (er) { $("csv-svar").textContent = er.message; });
        });
        $("slet-eksempler").addEventListener("click", function () {
            var btn = this;
            if (btn.getAttribute("data-sure") !== "1") { btn.setAttribute("data-sure", "1"); btn.textContent = "Tryk igen for at slette eksemplerne"; return; }
            adm("/api/admin/slet-eksempler", { method: "POST" }).then(function (r) { btn.textContent = r.slettet + " eksempler slettet"; });
        });
    }
})();

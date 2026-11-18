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
                    : r.antal + (r.antal === 1 ? " sted" : " steder") + (r.antal > r.steder.length ? " – de første " + r.steder.length + " vises" : "") + (pos ? ", nærmeste først." : ".");
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

    // ---------------- Ét sted ----------------
    if (kind === "sted") {
        var id = page.getAttribute("data-id");
        api("/api/steder/" + encodeURIComponent(id) + "?behov=" + encodeURIComponent(pas.join(","))).then(function (d) {
            var s = d.sted, m = d.match, paaTur = tur.indexOf(s.id) >= 0;
            document.title = s.navn + " – Easy-Pass";
            var adr = [s.adresse, [s.postnr, s.by].filter(Boolean).join(" ")].filter(Boolean).join(", ");
            var html = '<h1 class="ep-h1">' + esc(s.navn) + "</h1>"
                + '<p class="k-muted">' + esc(s.kategori) + (adr ? " · " + esc(adr) : "") + "</p>"
                + (s.kilde === "Eksempel" ? '<p class="ep-eksempel">Eksempel – ikke et rigtigt sted. Det viser, hvordan Easy-Pass virker.</p>' : "")
                + (s.beskrivelse ? "<p>" + esc(s.beskrivelse) + "</p>" : "");
            if (pas.length) {
                html += '<div class="ep-match">' + badge(m.pasning, m.ord)
                    + (m.mangler.length ? "<p><b>Mangler:</b> " + m.mangler.map(esc).join(", ") + "</p>" : "")
                    + (m.ukendt.length ? "<p><b>Ikke tjekket – spørg stedet:</b> " + m.ukendt.map(esc).join(", ") + "</p>" : "")
                    + (m.godt.length ? "<p><b>Har:</b> " + m.godt.map(esc).join(", ") + "</p>" : "") + "</div>";
            } else html += '<p class="ep-match">Sæt kryds i <a href="/pas">Mit pas</a>, så står der, om stedet passer til dig.</p>';
            html += '<h2 class="k-card-title">Hvad har stedet?</h2><table class="ep-table"><tbody>'
                + d.faciliteter.map(function (f) { return "<tr><th scope=\"row\">" + esc(f.navn) + '</th><td class="ep-svar-' + (f.svar === "Ja" ? "ja" : f.svar === "Nej" ? "nej" : "ukendt") + '">' + esc(f.svar) + "</td></tr>"; }).join("")
                + "</tbody></table>"
                + (s.sidstTjekket ? '<p class="k-muted">Sidst tjekket ' + new Date(s.sidstTjekket).toLocaleDateString("da-DK") + " · kilde: " + esc(s.kilde) + "</p>" : "")
                + '<div class="ep-row">'
                + (s.hjemmeside ? '<a class="k-btn k-btn-secondary" href="' + esc(/^https?:/.test(s.hjemmeside) ? s.hjemmeside : "https://" + s.hjemmeside) + '" target="_blank" rel="noopener">Stedets hjemmeside</a>' : "")
                + (s.telefon ? '<span class="ep-tlf">Telefon: ' + esc(s.telefon) + "</span>" : "")
                + '<button id="tur-knap" class="k-btn k-btn-primary" type="button">' + (paaTur ? "Fjern fra min tur" : "Læg på min tur") + "</button></div>";
            $("sted-kort").innerHTML = html;
            $("tur-knap").addEventListener("click", function () {
                paaTur = !paaTur;
                tur = paaTur ? tur.concat([s.id]) : tur.filter(function (x) { return x !== s.id; });
                save("ep-tur", tur); navCounts();
                this.textContent = paaTur ? "Fjern fra min tur" : "Læg på min tur";
            });
        }).catch(function (e) { $("sted-kort").innerHTML = "<p>" + esc(e.message) + '</p><a class="k-btn k-btn-secondary" href="/">Find steder</a>'; });

        function send(form, tekst, mail, svar, ejer) {
            $(form).addEventListener("submit", function (e) {
                e.preventDefault();
                $(svar).textContent = "Sender …";
                api("/api/steder/" + encodeURIComponent(id) + "/besked", {
                    method: "POST", headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({ tekst: $(tekst).value, mail: $(mail).value, ejer: ejer }),
                }).then(function () { $(svar).textContent = "Tak. Vi har fået det og vender tilbage."; $(tekst).value = ""; })
                  .catch(function (er) { $(svar).textContent = er.message; });
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
                    return '<div class="ep-sted' + (t.klaret ? " ep-klaret" : "") + '"><b>' + (t.type === "Ejer" ? "Stedet selv: " : "Fejl: ") + esc(t.sted) + "</b>"
                        + "<span>" + esc(t.tekst) + "</span><span class=\"k-muted\">" + new Date(t.tid).toLocaleString("da-DK") + (t.mail ? " · " + esc(t.mail) : "") + "</span>"
                        + (t.klaret ? '<span class="k-muted">Klaret</span>' : '<button class="k-btn k-btn-secondary" type="button" data-klaret="' + esc(t.id) + '">Klaret</button>') + "</div>";
                }).join("") : '<p class="k-muted">Ingen beskeder endnu.</p>';
            });
        }
        $("beskeder").addEventListener("click", function (e) {
            var b = e.target.closest("[data-klaret]"); if (!b) return;
            adm("/api/admin/beskeder/" + b.getAttribute("data-klaret") + "/klaret", { method: "POST" }).then(beskeder);
        });

        function start() {
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

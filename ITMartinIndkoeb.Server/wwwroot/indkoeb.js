// Ugens indkøb: one script for the start page and the family's four steps (data-page = start | uge | tilbud | seddel | handlet).
// The family's link is the access; "hvem" (your first name) is remembered in this browser only. Every storage call is
// wrapped - private windows may refuse it. The family pages refresh by themselves, so everyone sees the same week and list.
(function () {
    "use strict";
    var $ = function (id) { return document.getElementById(id); };
    var all = function (sel) { return [].slice.call(document.querySelectorAll(sel)); };
    var esc = function (s) { return String(s == null ? "" : s).replace(/[&<>"']/g, function (c) { return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]; }); };
    var kr = function (v) { return Math.round(v).toString().replace(/\B(?=(\d{3})+(?!\d))/g, "."); };
    var kr2 = function (v) { return (Math.round(v * 100) / 100).toFixed(v % 1 ? 2 : 0).replace(".", ","); };
    function api(url, opt) {
        return fetch(url, opt).then(function (r) {
            return r.text().then(function (t) {
                var body = null; try { body = t ? JSON.parse(t) : null; } catch (e) { }
                if (!r.ok) throw new Error((body && body.fejl) || "Noget gik galt. Prøv igen om lidt.");
                return body;
            });
        });
    }
    var json = function (method, body) { return { method: method, headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) }; };
    function get(k) { try { return localStorage.getItem(k) || ""; } catch (e) { return ""; } }
    function set(k, v) { try { localStorage.setItem(k, v); } catch (e) { } }
    var page = document.querySelector("[data-page]");
    if (!page) return;
    var kind = page.getAttribute("data-page");

    if (kind === "start") {
        $("ny-form").addEventListener("submit", function (e) {
            e.preventDefault();
            $("ny-svar").textContent = "Laver jeres side …";
            api("/api/husstand", json("POST", { navn: $("ny-navn").value }))
                .then(function (r) { location.href = r.link; })
                .catch(function (er) { $("ny-svar").textContent = er.message; });
        });
        return;
    }

    var key = page.getAttribute("data-key"), base = "/api/h/" + encodeURIComponent(key), home = "/h/" + key;
    var MDR = ["januar", "februar", "marts", "april", "maj", "juni", "juli", "august", "september", "oktober", "november", "december"];
    var data = null;
    var hvem = function () { return get("ik-hvem"); };
    var iso = function (d) { return d.getFullYear() + "-" + String(d.getMonth() + 1).padStart(2, "0") + "-" + String(d.getDate()).padStart(2, "0"); };
    var dagTekst = function (s) {
        var d = new Date(s + "T12:00:00"), i = new Date(); i.setHours(12, 0, 0, 0);
        var n = Math.round((d - i) / 864e5);
        return n === 0 ? "i dag" : n === 1 ? "i morgen" : d.toLocaleDateString("da-DK", { weekday: "long", day: "numeric", month: "short" });
    };
    // The next 8 days as choices: "i dag", "i morgen", "tirsdag 24. nov." …
    var dagValg = function (valgt) {
        var o = '<option value="">Hvornår?</option>';
        for (var k = 0; k < 8; k++) { var d = new Date(); d.setDate(d.getDate() + k); var v = iso(d); o += '<option value="' + v + '"' + (v === valgt ? " selected" : "") + ">" + dagTekst(v) + "</option>"; }
        return o;
    };

    // "Hvem er du?" - asked once on the steps that need a name, kept in this browser.
    function askName(then) {
        if (hvem()) return then(hvem());
        var box = $("ik-navn-boks");
        if (!box) {
            box = document.createElement("section");
            box.id = "ik-navn-boks"; box.className = "k-card ui-navn";
            box.innerHTML = '<form class="ui-form"><label for="ik-navn">Hvem er du? (dit fornavn)</label><div class="ui-row">'
                + '<input id="ik-navn" class="ui-input ui-grow" maxlength="30" autocomplete="given-name" /><button class="k-btn k-btn-primary" type="submit">OK</button></div></form>';
            page.insertBefore(box, page.firstChild);
        }
        box.hidden = false; box.scrollIntoView({ block: "center" }); $("ik-navn").focus();
        box.querySelector("form").onsubmit = function (e) {
            e.preventDefault();
            var n = $("ik-navn").value.trim(); if (!n) return;
            set("ik-hvem", n); box.hidden = true; then(n);
        };
    }

    function load() {
        return api(base).then(function (d) { data = d; draw(); })
            .catch(function (e) { var t = $("u-navn") || $("t-svar") || $("s-svar") || $("k-svar"); if (t) t.textContent = e.message; });
    }
    // Live enough for a family: every 20 s while the page is in front, and at once when it comes back.
    setInterval(function () { if (document.visibilityState === "visible" && kind !== "tilbud") load(); }, 20000);
    document.addEventListener("visibilitychange", function () { if (document.visibilityState === "visible") load(); });

    function draw() {
        if (kind === "uge") drawUge();
        if (kind === "uge" || kind === "handlet") drawListe();
        if (kind === "tilbud") drawTilbudSide();
        if (kind === "seddel") drawSeddel();
        if (kind === "handlet") drawHandlet();
        all("[data-sted] input").forEach(function (i) { if (!i.value && data.sted) i.placeholder = data.sted; });
    }

    // ---- 1 Ugen ----
    function drawUge() {
        var u = data.uge, m = data.maal;
        $("u-navn").textContent = data.navn + " · uge fra mandag " + new Date(u.mandag).toLocaleDateString("da-DK", { day: "numeric", month: "long" });
        $("u-tilbage").textContent = kr(u.tilbage);
        $("uge").setAttribute("data-state", u.tilstand === 0 ? "godt" : u.tilstand === 1 ? "paspaa" : "over");
        var pct = Math.max(0, Math.min(100, u.brugt / m.ugeMaal * 100));
        $("u-fill").style.width = pct + "%";
        $("u-bar").setAttribute("aria-valuemax", m.ugeMaal); $("u-bar").setAttribute("aria-valuenow", Math.round(u.brugt));
        $("u-brugt").textContent = kr(u.brugt) + " af " + kr(m.ugeMaal) + " kr brugt";
        $("u-ture").textContent = u.ture + " af " + m.maxTure + " ture (" + u.storeIndkoeb + " store, " + u.suppleringer + " suppleringer)";
        $("u-next").textContent = u.naesteSkridt;
        $("u-warn").innerHTML = u.advarsler.map(function (a) { return "<li>" + esc(a) + "</li>"; }).join("");
        var tilbage = 0, ture = [];
        data.seddel.forEach(function (g) {
            g.varer.forEach(function (v) { if (!v.koebt) tilbage++; });
            if (g.handler || g.dag) ture.push((g.handler ? g.handler + " handler i " : "") + (g.handler ? g.butik : g.butik) + (g.dag ? " " + dagTekst(g.dag) : ""));
        });
        $("u-seddel").innerHTML = '<a class="k-btn k-btn-secondary" href="' + home + '/seddel">Indkøbssedlen: ' + tilbage + (tilbage === 1 ? " vare" : " varer") + "</a>"
            + (ture.length ? " <span>" + esc(ture.join(" · ")) + "</span>" : "");

        $("m-maal").textContent = "Mål: " + kr(m.maanedFra) + "–" + kr(m.maanedTil) + " kr om måneden.";
        $("maaneder").innerHTML = data.maaneder.map(function (x) {
            var ok = x.brugt <= x.til, cls = !x.faerdig ? "" : ok ? "ok" : "over";
            var txt = !x.faerdig ? "i gang" : x.brugt < x.fra ? "under målet" : ok ? "inde i målet" : "over målet";
            return '<div class="ui-item ' + cls + '"><div><b>' + MDR[x.maaned - 1] + " " + x.aar + "</b><span>" + x.ture + " ture · " + txt + "</span></div><b>" + kr(x.brugt) + " kr</b></div>";
        }).join("");
        if (!$("m-uge").value) {
            $("m-navn").value = data.navn; $("m-uge").value = m.ugeMaal; $("m-ture").value = m.maxTure;
            $("m-suppl").value = m.supplGraense; $("m-stort").value = m.stortFra;
        }
        $("del-link").value = location.origin + home;
    }

    function drawListe() {
        $("liste").innerHTML = data.denneUge.length ? data.denneUge.map(function (k) {
            return '<div class="ui-item"><div><b>' + kr(k.beloeb) + " kr · " + esc(k.butik) + "</b><span>" + esc(k.type) + (k.overGraense ? " · over grænsen" : "") + " · "
                + new Date(k.dato).toLocaleDateString("da-DK", { weekday: "short", day: "numeric", month: "short" }) + (k.hvem ? " · " + esc(k.hvem) : "") + "</span></div>"
                + '<button class="k-btn k-btn-ghost k-btn-sm" type="button" data-slet="' + esc(k.id) + '">Fjern</button></div>';
        }).join("") : '<p class="k-muted">Ingen indkøb endnu i denne uge.</p>';
    }
    if ($("liste")) $("liste").addEventListener("click", function (e) {
        var b = e.target.closest("[data-slet]"); if (!b) return;
        if (b.getAttribute("data-sure") !== "1") { b.setAttribute("data-sure", "1"); b.textContent = "Tryk igen"; return; }
        api(base + "/koeb/" + encodeURIComponent(b.getAttribute("data-slet")), { method: "DELETE" }).then(load);
    });

    if ($("maal-form")) $("maal-form").addEventListener("submit", function (e) {
        e.preventDefault();
        var n = function (id) { return parseFloat(($(id).value || "").replace(",", ".")) || 0; };
        api(base + "/maal", json("PUT", { navn: $("m-navn").value, ugeMaal: n("m-uge"), maxTure: Math.round(n("m-ture")), supplGraense: n("m-suppl"), stortFra: n("m-stort") }))
            .then(function () { $("maal-svar").textContent = "Gemt."; return load(); })
            .catch(function (er) { $("maal-svar").textContent = er.message; });
    });
    if ($("del-kopier")) $("del-kopier").addEventListener("click", function () {
        var b = this;
        try { navigator.clipboard.writeText($("del-link").value).then(function () { b.textContent = "Kopieret"; }, function () { $("del-link").select(); }); } catch (e) { $("del-link").select(); }
    });

    // Postnummer (Ugemål og sted + the first visit to Find tilbud).
    all("[data-sted]").forEach(function (f) {
        f.addEventListener("submit", function (e) {
            e.preventDefault();
            var svar = f.querySelector("[data-sted-svar]");
            svar.textContent = "Finder stedet …";
            api(base + "/sted", json("PUT", { postnr: f.querySelector("input").value.trim() }))
                .then(function () { svar.textContent = "Gemt."; return load(); })
                .catch(function (er) { svar.textContent = er.message; });
        });
    });

    // ---- 2 Find tilbud ----
    var sidste = [];
    function drawTilbudSide() {
        $("sted-boks").hidden = !!data.sted;
        $("t-sted").textContent = data.sted ? "Butikker inden for 10 km af " + data.sted + "." : "";
        var kendte = ["Lidl Plus", "Netto+", "føtex Plus", "Bilka Plus", "Coop-appen", "REMA-appen"];
        $("apps").innerHTML = kendte.concat(data.apps.filter(function (a) { return kendte.indexOf(a) < 0; })).map(function (a) {
            var har = data.apps.indexOf(a) >= 0;
            return '<button type="button" class="k-btn ' + (har ? "k-btn-primary" : "k-btn-secondary") + ' k-btn-sm" aria-pressed="' + har + '" data-app="' + esc(a) + '">' + (har ? "Har " : "") + esc(a) + "</button>";
        }).join("");
        $("skjult-boks").hidden = !data.skjulte.length;
        $("skjulte").innerHTML = data.skjulte.map(function (b) { return '<button type="button" class="k-btn k-btn-secondary k-btn-sm" data-vis="' + esc(b) + '">Vis ' + esc(b) + " igen</button>"; }).join("");
        if (!$("t-forslag").children.length) {
            var paa = [];
            data.seddel.forEach(function (g) { if (g.butik === "Hvor som helst") g.varer.forEach(function (v) { if (!v.koebt) paa.push(v.tekst); }); });
            var ord = paa.concat(["kaffe", "mælk", "smør", "kylling", "hakket oksekød", "æg"]).filter(function (x, i, a) { return a.indexOf(x) === i; }).slice(0, 10);
            $("t-forslag").innerHTML = ord.map(function (w) { return '<button type="button" class="k-btn k-btn-ghost k-btn-sm" data-q="' + esc(w) + '">' + esc(w) + "</button>"; }).join("");
        }
    }
    function soeg(q) {
        $("t-q").value = q;
        $("t-svar").textContent = "Søger i ugens tilbud …"; $("t-liste").innerHTML = "";
        api(base + "/tilbud?q=" + encodeURIComponent(q)).then(function (list) {
            sidste = list;
            var kort = function (o) {
                var i = list.indexOf(o);
                var til = o.til ? "til " + new Date(o.til).toLocaleDateString("da-DK", { weekday: "short", day: "numeric", month: "short" }) : "";
                return '<article class="ui-offer">' + (o.billede ? '<img src="' + esc(o.billede) + '" alt="" loading="lazy" referrerpolicy="no-referrer" />' : '<span class="ui-offer-img"></span>')
                    + '<div class="ui-offer-txt"><b>' + esc(o.butik) + " · " + kr2(o.voresPris != null ? o.voresPris : o.pris) + " kr</b>"
                    + (o.prisNote ? '<em class="ui-app' + (o.manglerApp ? " mangler" : "") + '">' + esc(o.prisNote) + "</em>" : "")
                    + "<span>" + esc(o.titel) + "</span>"
                    + "<small>" + [o.enhedspris, o.foer ? "før " + kr2(o.foer) + " kr" : "", til, o.naermesteButik ? o.naermesteButik + (o.km ? " (" + o.km.toFixed(1).replace(".", ",") + " km)" : "") : ""]
                        .filter(Boolean).map(esc).join(" · ") + "</small>"
                    + (o.app && (o.manglerApp || (o.appPris != null && data.apps.indexOf(o.app) < 0)) ? '<button class="k-btn k-btn-ghost k-btn-sm ui-harapp" type="button" data-harapp="' + esc(o.app) + '">Vi har ' + esc(o.app) + "</button>" : "")
                    + "</div>"
                    + '<div class="ui-offer-btns"><button class="k-btn k-btn-primary k-btn-sm" type="button" data-paa="' + i + '">Sæt på sedlen</button>'
                    + '<button class="k-btn k-btn-ghost k-btn-sm" type="button" data-skjul="' + esc(o.butik) + '" title="Vi handler aldrig i ' + esc(o.butik) + '">Skjul ' + esc(o.butik) + "</button></div></article>";
            };
            // Specific (user 2026-11-19): only the thing itself; kakaomælk, tykmælk, laktosefri … are folded away.
            var main = list.filter(function (o) { return !o.andenVare; }), andre = list.filter(function (o) { return o.andenVare; });
            $("t-svar").textContent = main.length ? main.length + " tilbud på \"" + q + "\" – billigst først."
                : "Ingen tilbud på \"" + q + "\" i denne uge. Sæt den på sedlen alligevel?";
            $("t-liste").innerHTML = main.map(kort).join("")
                + (main.length ? "" : '<button class="k-btn k-btn-secondary" type="button" data-paa="-1">Sæt "' + esc(q) + '" på sedlen</button>')
                + (andre.length ? '<details class="ui-andre"><summary>Andre varer med "' + esc(q) + '" (' + andre.length + ")</summary>" + andre.map(kort).join("") + "</details>" : "");
        }).catch(function (er) { $("t-svar").textContent = er.message; });
    }
    if (kind === "tilbud") {
        $("t-form").addEventListener("submit", function (e) { e.preventDefault(); var q = $("t-q").value.trim(); if (q) soeg(q); });
        $("t-forslag").addEventListener("click", function (e) { var b = e.target.closest("[data-q]"); if (b) soeg(b.getAttribute("data-q")); });
        var skjul = function (butik, ja) {
            return api(base + "/skjul", json("PUT", { butik: butik, skjul: ja })).then(load).then(function () { var q = $("t-q").value.trim(); if (q) soeg(q); });
        };
        var app = function (navn, har) {
            return api(base + "/apps", json("PUT", { app: navn, har: har })).then(load).then(function () { var q = $("t-q").value.trim(); if (q) soeg(q); });
        };
        $("apps").addEventListener("click", function (e) { var b = e.target.closest("[data-app]"); if (b) app(b.getAttribute("data-app"), b.getAttribute("aria-pressed") !== "true"); });
        $("skjulte").addEventListener("click", function (e) { var b = e.target.closest("[data-vis]"); if (b) skjul(b.getAttribute("data-vis"), false); });
        $("t-liste").addEventListener("click", function (e) {
            var s = e.target.closest("[data-skjul]"); if (s) { skjul(s.getAttribute("data-skjul"), true); return; }
            var ha = e.target.closest("[data-harapp]"); if (ha) { app(ha.getAttribute("data-harapp"), true); return; }
            var b = e.target.closest("[data-paa]"); if (!b) return;
            var i = +b.getAttribute("data-paa"), o = sidste[i];
            var vare = o ? { tekst: o.titel, butik: o.butik, tilbud: kr2(o.voresPris != null ? o.voresPris : o.pris) + " kr" + (o.app && o.prisNote && o.prisNote.indexOf("med " + o.app) === 0 ? " med " + o.app : "") + (o.til ? ", til " + new Date(o.til).toLocaleDateString("da-DK", { weekday: "long" }) : "") + (o.manglerApp ? ", kun med " + o.app : "") }
                : { tekst: $("t-q").value.trim(), butik: null, tilbud: null };
            askName(function (n) {
                vare.hvem = n;
                api(base + "/seddel", json("POST", vare)).then(function () { b.textContent = "På sedlen"; b.disabled = true; b.className = "k-btn k-btn-secondary k-btn-sm"; })
                    .catch(function (er) { $("t-svar").textContent = er.message; });
            });
        });
    }

    // ---- 3 Indkøbsseddel ----
    function drawSeddel() {
        var sel = $("s-butik");
        if (!$("s-dag").options.length) $("s-dag").innerHTML = dagValg("").replace("Hvornår?", "Når det passer");
        if (sel.options.length < 2) data.butikker.filter(function (b) { return b !== "Andet"; }).forEach(function (b) { var o = document.createElement("option"); o.value = b; o.textContent = b; sel.appendChild(o); });
        var mig = hvem();
        $("s-grupper").innerHTML = data.seddel.length ? data.seddel.map(function (g) {
            var aabne = g.varer.filter(function (v) { return !v.koebt; }).length;
            var dag = g.dag ? " " + dagTekst(g.dag) : "";
            var hvornaar = '<label class="ui-dag"><span class="ui-sr">Hvornår handles der' + (g.butik === "Hvor som helst" ? "" : " i " + esc(g.butik)) + '?</span><select class="ui-input" data-dag="' + esc(g.butik) + '">' + dagValg(g.dag) + "</select></label>";
            var tur = g.handler
                ? '<div class="ui-tur ' + (g.handler === mig ? "mig" : "") + '"><b>' + esc((g.handler === mig ? "Du handler her" : g.handler + " handler her") + dag) + "</b>"
                    + hvornaar + (g.handler === mig ? ' <button class="k-btn k-btn-ghost k-btn-sm" type="button" data-slip="' + esc(g.butik) + '">Slip turen</button>' : "") + "</div>"
                : (aabne ? '<div class="ui-tur ledig">' + (g.dag ? "<b>Planlagt" + esc(dag) + "</b>" : "") + hvornaar
                    + '<button class="k-btn k-btn-secondary" type="button" data-tag="' + esc(g.butik) + '">Jeg handler ' + (g.butik === "Hvor som helst" ? "det" : "i " + esc(g.butik)) + "</button></div>" : "");
            return '<section class="k-card ui-group"><h2 class="k-card-title">' + esc(g.butik) + ' <small>' + aabne + (aabne === 1 ? " vare" : " varer") + "</small></h2>" + tur
                + '<ul class="ui-check">' + g.varer.map(function (v) {
                    return '<li class="' + (v.koebt ? "koebt" : "") + '"><label><input type="checkbox" data-kryds="' + esc(v.id) + '"' + (v.koebt ? " checked" : "") + " /><span><b>" + esc(v.tekst) + "</b>"
                        + (v.tilbud || v.hvem ? "<small>" + esc([v.tilbud ? "tilbud " + v.tilbud : "", v.hvem ? "skrevet af " + v.hvem : ""].filter(Boolean).join(" · ")) + "</small>" : "") + "</span></label>"
                        + '<button class="k-btn k-btn-ghost k-btn-sm" type="button" data-fjern="' + esc(v.id) + '" aria-label="Fjern ' + esc(v.tekst) + '">Fjern</button></li>';
                }).join("") + "</ul>"
                + (g.varer.some(function (v) { return v.koebt; }) ? '<a class="k-btn k-btn-primary" href="' + home + '/handlet">Færdig? Gem købet</a>' : "")
                + "</section>";
        }).join("") : '<section class="k-card"><p class="k-muted">Sedlen er tom. Skriv en vare ovenfor, eller <a href="' + home + '/tilbud">find ugens tilbud</a>.</p></section>';
    }
    if (kind === "seddel") {
        $("s-form").addEventListener("submit", function (e) {
            e.preventDefault();
            var t = $("s-tekst").value.trim(); if (!t) { $("s-svar").textContent = "Skriv hvad der skal købes, fx mælk."; return; }
            askName(function (n) {
                var butik = $("s-butik").value || null, dag = $("s-dag").value || null;
                api(base + "/seddel", json("POST", { tekst: t, hvem: n, butik: butik }))
                    .then(function () {
                        // A day = that shop's trip is planned for that day ("mælk i Lidl tirsdag"); who takes it stays as it is.
                        if (!dag) return;
                        var g = data.seddel.filter(function (x) { return x.butik === (butik || "Hvor som helst"); })[0];
                        return api(base + "/tur", json("PUT", { butik: butik || "Hvor som helst", hvem: g && g.handler || "", dag: dag }));
                    })
                    .then(function () { $("s-tekst").value = ""; $("s-svar").textContent = ""; $("s-tekst").focus(); return load(); })
                    .catch(function (er) { $("s-svar").textContent = er.message; });
            });
        });
        $("s-grupper").addEventListener("change", function (e) {
            var d = e.target.closest("[data-dag]");
            if (d) {
                var g = data.seddel.filter(function (x) { return x.butik === d.getAttribute("data-dag"); })[0];
                api(base + "/tur", json("PUT", { butik: d.getAttribute("data-dag"), hvem: g && g.handler || "", dag: d.value || null })).then(load);
                return;
            }
            var c = e.target.closest("[data-kryds]"); if (!c) return;
            api(base + "/seddel/" + encodeURIComponent(c.getAttribute("data-kryds")), json("PUT", { koebt: c.checked })).then(load);
        });
        $("s-grupper").addEventListener("click", function (e) {
            var b;
            if ((b = e.target.closest("[data-tag]"))) {
                var g0 = data.seddel.filter(function (g) { return g.butik === b.getAttribute("data-tag"); })[0];
                askName(function (n) { api(base + "/tur", json("PUT", { butik: b.getAttribute("data-tag"), hvem: n, dag: g0 && g0.dag || null })).then(load); });
            }
            else if ((b = e.target.closest("[data-slip]"))) api(base + "/tur", json("PUT", { butik: b.getAttribute("data-slip"), hvem: "" })).then(load);
            else if ((b = e.target.closest("[data-fjern]"))) api(base + "/seddel/" + encodeURIComponent(b.getAttribute("data-fjern")), { method: "DELETE" }).then(load);
        });
    }

    // ---- 4 Har handlet ----
    function drawHandlet() {
        var u = data.uge;
        $("kv-boks").hidden = !data.kvittering;
        $("h-tilbage").textContent = kr(u.tilbage) + " kr tilbage denne uge · " + u.ture + " af " + data.maal.maxTure + " ture.";
        if (!$("k-butikker").children.length) {
            // The trip you took comes first - that is almost always where you just were.
            var mine = data.seddel.filter(function (g) { return g.handler && g.handler === hvem(); }).map(function (g) { return g.butik; });
            var list = mine.filter(function (b) { return data.butikker.indexOf(b) >= 0; }).concat(data.butikker.filter(function (b) { return mine.indexOf(b) < 0; }));
            $("k-butikker").innerHTML = list.map(function (b) { return '<button type="button" class="k-btn k-btn-secondary ui-shop" data-b="' + esc(b) + '">' + esc(b) + "</button>"; }).join("");
        }
    }
    if (kind === "handlet") {
        $("k-hvem").value = hvem();
        $("k-hvem").addEventListener("change", function () { set("ik-hvem", this.value.trim()); });
        // A picture is enough: read + saved at once. No total found -> type the amount, the shop is marked.
        $("kv-fil").addEventListener("change", function () {
            var f = this.files[0]; if (!f) return;
            var fd = new FormData(); fd.append("billede", f, f.name || "kvittering.jpg"); fd.append("hvem", $("k-hvem").value.trim() || hvem());
            $("kv-svar").textContent = "Læser kvitteringen … (op til et halvt minut)";
            all(".ui-shop").forEach(function (b) { b.classList.remove("forslag"); });
            api(base + "/kvittering", { method: "POST", body: fd }).then(function (r) {
                if (r.gemt) {
                    $("kv-svar").textContent = "Gemt: " + kr(r.beloeb) + " kr i " + (r.butik || "Andet")
                        + (r.dato ? " · " + new Date(r.dato).toLocaleDateString("da-DK", { day: "numeric", month: "short" }) : "") + ". Forkert? Tryk Fjern herunder.";
                    return load();
                }
                $("kv-svar").textContent = "Totalen kunne ikke læses. Skriv beløbet, og tryk på butikken.";
                var btn = document.querySelector('.ui-shop[data-b="' + (r.butik || "Andet") + '"]'); if (btn) btn.classList.add("forslag");
            }).catch(function (er) { $("kv-svar").textContent = er.message; })
              .then(function () { $("kv-fil").value = ""; });
        });
        $("k-butikker").addEventListener("click", function (e) {
            var b = e.target.closest("[data-b]"); if (!b) return;
            var beloeb = parseFloat(($("k-beloeb").value || "").replace(/\./g, "").replace(",", "."));
            if (!(beloeb > 0)) { $("k-svar").textContent = "Skriv beløbet først, fx 245 – eller tag et billede af kvitteringen."; $("k-beloeb").focus(); return; }
            var n = $("k-hvem").value.trim(); if (n) set("ik-hvem", n);
            $("k-svar").textContent = "Gemmer …";
            api(base + "/koeb", json("POST", { beloeb: beloeb, butik: b.getAttribute("data-b"), hvem: n, dato: $("k-dato").value || null }))
                .then(function () {
                    $("k-beloeb").value = ""; $("k-dato").value = ""; $("kv-svar").textContent = "";
                    all(".ui-shop").forEach(function (x) { x.classList.remove("forslag"); });
                    $("k-svar").textContent = "Gemt: " + kr(beloeb) + " kr i " + b.getAttribute("data-b") + ".";
                    return load();
                })
                .catch(function (er) { $("k-svar").textContent = er.message; });
        });
        $("koeb-form").addEventListener("submit", function (e) { e.preventDefault(); $("k-svar").textContent = "Tryk på butikken for at gemme."; });
    }

    load();
})();

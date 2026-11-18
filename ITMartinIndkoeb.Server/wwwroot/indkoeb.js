// Ugens indkøb: one script for both pages (data-page). The family's link is the access; "hvem" is remembered in this
// browser only. Every storage call is wrapped - private windows may refuse it.
(function () {
    "use strict";
    var $ = function (id) { return document.getElementById(id); };
    var esc = function (s) { return String(s == null ? "" : s).replace(/[&<>"']/g, function (c) { return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]; }); };
    var kr = function (v) { return Math.round(v).toString().replace(/\B(?=(\d{3})+(?!\d))/g, "."); };
    function api(url, opt) {
        return fetch(url, opt).then(function (r) {
            return r.text().then(function (t) {
                var body = t ? JSON.parse(t) : null;
                if (!r.ok) throw new Error((body && body.fejl) || "Noget gik galt. Prøv igen om lidt.");
                return body;
            });
        });
    }
    function get(k) { try { return localStorage.getItem(k) || ""; } catch (e) { return ""; } }
    function set(k, v) { try { localStorage.setItem(k, v); } catch (e) { } }
    var page = document.querySelector("[data-page]");
    if (!page) return;
    var kind = page.getAttribute("data-page");

    if (kind === "start") {
        $("ny-form").addEventListener("submit", function (e) {
            e.preventDefault();
            $("ny-svar").textContent = "Laver jeres side …";
            api("/api/husstand", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ navn: $("ny-navn").value }) })
                .then(function (r) { location.href = r.link; })
                .catch(function (er) { $("ny-svar").textContent = er.message; });
        });
        return;
    }

    var key = page.getAttribute("data-key"), base = "/api/h/" + encodeURIComponent(key);
    var MDR = ["januar", "februar", "marts", "april", "maj", "juni", "juli", "august", "september", "oktober", "november", "december"];
    var data = null;
    $("k-hvem").value = get("ik-hvem");
    $("del-link").value = location.origin + "/h/" + key;
    $("del-kopier").addEventListener("click", function () {
        var b = this;
        try { navigator.clipboard.writeText($("del-link").value).then(function () { b.textContent = "Kopieret"; }, function () { $("del-link").select(); }); } catch (e) { $("del-link").select(); }
    });

    function load() {
        return api(base).then(function (d) { data = d; draw(); })
            .catch(function (e) { $("u-navn").textContent = e.message; });
    }

    function draw() {
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

        if (!$("k-butikker").children.length)
            $("k-butikker").innerHTML = data.butikker.map(function (b) { return '<button type="button" class="k-btn k-btn-secondary ui-shop" data-b="' + esc(b) + '">' + esc(b) + "</button>"; }).join("");

        $("liste").innerHTML = data.denneUge.length ? data.denneUge.map(function (k) {
            return '<div class="ui-item"><div><b>' + kr(k.beloeb) + " kr · " + esc(k.butik) + "</b><span>" + esc(k.type) + (k.overGraense ? " · over grænsen" : "") + " · "
                + new Date(k.dato).toLocaleDateString("da-DK", { weekday: "short", day: "numeric", month: "short" }) + (k.hvem ? " · " + esc(k.hvem) : "") + "</span></div>"
                + '<button class="k-btn k-btn-ghost k-btn-sm" type="button" data-slet="' + esc(k.id) + '">Fjern</button></div>';
        }).join("") : '<p class="k-muted">Ingen indkøb endnu i denne uge.</p>';

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
    }

    // One press: amount typed, then the shop button saves it.
    $("k-butikker").addEventListener("click", function (e) {
        var b = e.target.closest("[data-b]"); if (!b) return;
        var beloeb = parseFloat(($("k-beloeb").value || "").replace(/\./g, "").replace(",", "."));
        if (!(beloeb > 0)) { $("k-svar").textContent = "Skriv beløbet først, fx 245."; $("k-beloeb").focus(); return; }
        var hvem = $("k-hvem").value.trim(); set("ik-hvem", hvem);
        $("k-svar").textContent = "Gemmer …";
        api(base + "/koeb", { method: "POST", headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ beloeb: beloeb, butik: b.getAttribute("data-b"), hvem: hvem, dato: $("k-dato").value || null }) })
            .then(function () { $("k-beloeb").value = ""; $("k-svar").textContent = "Gemt: " + kr(beloeb) + " kr i " + b.getAttribute("data-b") + "."; return load(); })
            .catch(function (er) { $("k-svar").textContent = er.message; });
    });
    $("koeb-form").addEventListener("submit", function (e) { e.preventDefault(); $("k-svar").textContent = "Tryk på butikken for at gemme."; });

    $("liste").addEventListener("click", function (e) {
        var b = e.target.closest("[data-slet]"); if (!b) return;
        if (b.getAttribute("data-sure") !== "1") { b.setAttribute("data-sure", "1"); b.textContent = "Tryk igen"; return; }
        api(base + "/koeb/" + encodeURIComponent(b.getAttribute("data-slet")), { method: "DELETE" }).then(load);
    });

    $("maal-form").addEventListener("submit", function (e) {
        e.preventDefault();
        var n = function (id) { return parseFloat(($(id).value || "").replace(",", ".")) || 0; };
        api(base + "/maal", { method: "PUT", headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ navn: $("m-navn").value, ugeMaal: n("m-uge"), maxTure: Math.round(n("m-ture")), supplGraense: n("m-suppl"), stortFra: n("m-stort") }) })
            .then(function () { $("maal-svar").textContent = "Gemt."; return load(); })
            .catch(function (er) { $("maal-svar").textContent = er.message; });
    });

    load();
})();

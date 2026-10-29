// Kolibri Fællesskab (2026-10-29): the overview of communities, one community (right now, tips, chat), login with the
// kundenummer, and Martin's "right now" form. Plain JS like Snak; every text from the server goes in with textContent.
(function () {
    "use strict";
    var $ = function (id) { return document.getElementById(id); };
    var state = { me: { loggedIn: false, name: "", admin: false }, slug: null, view: null, lastId: 0, timer: null };

    function api(method, url, body) {
        return fetch(url, {
            method: method, credentials: "same-origin",
            headers: body ? { "Content-Type": "application/json" } : {},
            body: body ? JSON.stringify(body) : undefined
        }).then(function (r) {
            return r.text().then(function (t) {
                var data = null; try { data = t ? JSON.parse(t) : null; } catch (e) { }
                if (!r.ok) { var err = new Error((data && data.fejl) || "Det gik ikke – prøv igen om lidt."); err.status = r.status; throw err; }
                return data;
            });
        });
    }

    function el(tag, cls, text) {
        var e = document.createElement(tag);
        if (cls) e.className = cls;
        if (text != null) e.textContent = text;
        return e;
    }

    function show(id, on) { $(id).hidden = !on; }

    // ---- overview ----

    function loadList() {
        return api("GET", "/api/faellesskaber").then(function (list) {
            var box = $("liste"); box.textContent = "";
            list.forEach(function (c) {
                var b = el("button", "fk-card"); b.type = "button";
                b.appendChild(el("b", "fk-card-title", c.title));
                b.appendChild(el("span", "fk-card-line", c.line));
                if (c.now) b.appendChild(el("span", "fk-card-now", c.nowLabel + ": " + c.now.title + (c.now.when ? " · " + c.now.when : "")));
                b.appendChild(el("span", "fk-card-count", (c.members === 1 ? "1 er med" : c.members + " er med") + (c.joined ? " · du er med" : "")));
                b.addEventListener("click", function () { open(c.slug, true); });
                box.appendChild(b);
            });
        }).catch(function (e) { $("liste").textContent = e.message; });
    }

    // ---- one community ----

    function open(slug, push) {
        state.slug = slug; state.lastId = 0;
        if (push) history.pushState({ f: slug }, "", "?f=" + encodeURIComponent(slug));
        show("oversigt", false); show("side", true); window.scrollTo(0, 0);
        $("beskeder").textContent = "";
        return api("GET", "/api/faellesskaber/" + encodeURIComponent(slug)).then(function (c) {
            state.view = c;
            document.title = c.title + " · Kolibri Fællesskab";
            $("sTitel").textContent = c.title;
            $("sLinje").textContent = c.line;
            $("sAktivitet").textContent = c.activity;
            renderJoin(); renderNow();
            if (c.hasTips) loadTips(); else show("tipsBoks", false);
            renderChat();
        }).catch(function () { home(true); });
    }

    function renderJoin() {
        var c = state.view;
        $("sMedlemmer").textContent = c.members === 1 ? "1 er med" : c.members + " er med";
        $("medKnap").textContent = c.joined ? "Du er med – tryk for at gå ud" : "Jeg er med";
    }

    function renderNow() {
        var c = state.view, box = $("nu"); box.textContent = "";
        $("nuLabel").textContent = c.nowLabel;
        if (c.now) {
            box.appendChild(el("p", "fk-now-title", c.now.title));
            if (c.now.when) box.appendChild(el("p", "fk-now-when", "Vi mødes: " + c.now.when));
            if (c.now.note) box.appendChild(el("p", "fk-now-note", c.now.note));
        } else {
            box.appendChild(el("p", "k-help", "Martin sætter snart det første på. Tryk \"Jeg er med\", så kan du se det, når du kommer igen."));
        }
        show("nuForm", state.me.admin);
        if (state.me.admin) {
            $("nuTitel").value = c.now ? c.now.title : "";
            $("nuHvornaar").value = c.now ? c.now.when : "";
            $("nuNote").value = c.now ? c.now.note : "";
        }
    }

    function loadTips() {
        show("tipsBoks", true);
        var ul = $("tips"); ul.textContent = "";
        ul.appendChild(el("li", "k-help", "Henter tips…"));
        api("GET", "/api/faellesskaber/" + encodeURIComponent(state.slug) + "/tips").then(function (tips) {
            ul.textContent = "";
            if (!tips.length) { ul.appendChild(el("li", "k-help", "Ingen tips lige nu – prøv igen senere.")); return; }
            tips.forEach(function (t) {
                var li = el("li"), a = el("a", "fk-tip");
                a.href = t.url; a.target = "_blank"; a.rel = "noopener nofollow";
                if (t.image) {
                    var img = el("img"); img.src = t.image; img.alt = ""; img.loading = "lazy";
                    img.addEventListener("error", function () { img.remove(); });
                    a.appendChild(img);
                }
                var txt = el("span", "fk-tip-text");
                txt.appendChild(el("b", null, t.title));
                if (t.line) txt.appendChild(el("span", null, t.line));
                a.appendChild(txt);
                li.appendChild(a); ul.appendChild(li);
            });
        }).catch(function () { ul.textContent = ""; ul.appendChild(el("li", "k-help", "Tipsene kan ikke hentes lige nu.")); });
    }

    // ---- chat ----

    function renderChat() {
        var inside = state.me.loggedIn || state.me.admin;
        show("loginBoks", !inside); show("chat", inside);
        show("navnForm", state.me.loggedIn && !state.me.name);
        show("skriv", state.me.loggedIn);
        clearInterval(state.timer);
        if (inside) { loadMessages(); state.timer = setInterval(loadMessages, 15000); }
    }

    function loadMessages() {
        if (!state.slug) return;
        api("GET", "/api/faellesskaber/" + encodeURIComponent(state.slug) + "/beskeder?efter=" + state.lastId).then(function (list) {
            var box = $("beskeder");
            list.forEach(function (m) {
                state.lastId = Math.max(state.lastId, m.id);
                var d = el("div", "fk-msg" + (m.name === state.me.name ? " mig" : ""));
                d.appendChild(el("span", "fk-msg-meta", m.name + " · " + new Date(m.at).toLocaleString("da-DK", { day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" })));
                d.appendChild(el("span", "fk-msg-text", m.text));
                box.insertBefore(d, box.firstChild);   // newest at the top, right under the write box
            });
            show("tomChat", box.children.length === 0);
        }).catch(function (e) { if (e.status === 401) { state.me.loggedIn = false; renderChat(); } });
    }

    function home(push) {
        state.slug = null; clearInterval(state.timer);
        if (push) history.pushState({}, "", location.pathname);
        document.title = "Kolibri Fællesskab";
        show("side", false); show("oversigt", true);
        loadList();
    }

    function error(id, msg) { $(id).textContent = msg || ""; show(id, !!msg); }

    // ---- events ----

    $("tilbage").addEventListener("click", function () { home(true); });
    window.addEventListener("popstate", function () {
        var f = new URLSearchParams(location.search).get("f");
        if (f) open(f, false); else home(false);
    });

    $("medKnap").addEventListener("click", function () {
        if (!state.me.loggedIn) { $("chatBoks").scrollIntoView({ behavior: "smooth" }); $("kode").focus(); return; }
        api("POST", "/api/faellesskaber/" + encodeURIComponent(state.slug) + "/med").then(function (r) {
            state.view.members += r.joined ? 1 : -1; state.view.joined = r.joined; renderJoin();
        });
    });

    $("loginForm").addEventListener("submit", function (e) {
        e.preventDefault(); error("loginFejl", "");
        api("POST", "/api/login", { kode: $("kode").value, navn: $("navn").value }).then(function () { return refreshMe(); })
            .then(function () { renderNow(); renderChat(); return api("GET", "/api/faellesskaber/" + encodeURIComponent(state.slug)); })
            .then(function (c) { state.view = c; renderJoin(); })
            .catch(function (err) { error("loginFejl", err.message); });
    });

    $("navnForm").addEventListener("submit", function (e) {
        e.preventDefault();
        api("POST", "/api/navn", { navn: $("nytNavn").value }).then(function (r) { state.me.name = r.name; renderChat(); })
            .catch(function (err) { error("skrivFejl", err.message); });
    });

    $("skriv").addEventListener("submit", function (e) {
        e.preventDefault(); error("skrivFejl", "");
        var t = $("tekst").value.trim(); if (!t) return;
        api("POST", "/api/faellesskaber/" + encodeURIComponent(state.slug) + "/beskeder", { tekst: t })
            .then(function () { $("tekst").value = ""; loadMessages(); })
            .catch(function (err) { error("skrivFejl", err.message); });
    });

    $("nuForm").addEventListener("submit", function (e) {
        e.preventDefault();
        api("PUT", "/api/faellesskaber/" + encodeURIComponent(state.slug) + "/nu", { titel: $("nuTitel").value, note: $("nuNote").value, hvornaar: $("nuHvornaar").value })
            .then(function (n) { state.view.now = { title: n.title, note: n.note, when: n.when }; renderNow(); if (state.view.hasTips) loadTips(); });
    });
    $("nuFjern").addEventListener("click", function () {
        api("DELETE", "/api/faellesskaber/" + encodeURIComponent(state.slug) + "/nu").then(function () { state.view.now = null; renderNow(); });
    });

    $("logUd").addEventListener("click", function () {
        api("POST", "/api/logout").then(function () { state.me = { loggedIn: false, name: "", admin: false }; renderNow(); renderChat(); });
    });

    function refreshMe() { return api("GET", "/api/me").then(function (m) { state.me = m; }).catch(function () { }); }

    refreshMe().then(function () {
        var f = new URLSearchParams(location.search).get("f");
        if (f) open(f, false); else loadList();
    });
})();

// Photo queue (2026-10-19). Photos used to travel over the Blazor connection, and phones drop that connection while
// the camera app is open - the page reloaded and the photo was gone without a word. Now every photo is first kept in
// the phone's own storage (IndexedDB), then sent with a plain upload, retried until the server has it - also after a
// reload. Since 2026-11-15 only uploads ("only upload from photos"): photos are taken with the phone's own camera.
window.photoQueue = {
    db: null,
    sending: false,
    listeners: [],
    failed: 0,
    rejected: null,

    open() {
        if (this.db) return Promise.resolve(this.db);
        var self = this;
        return new Promise(function (resolve, reject) {
            var req = indexedDB.open("vareinfo-fotos", 1);
            req.onupgradeneeded = function () { req.result.createObjectStore("koe", { keyPath: "id", autoIncrement: true }); };
            req.onsuccess = function () { self.db = req.result; resolve(self.db); };
            req.onerror = function () { reject(req.error); };
        });
    },

    async tx(mode, work) {
        var db = await this.open();
        return new Promise(function (resolve, reject) {
            var t = db.transaction("koe", mode);
            var r = work(t.objectStore("koe"));
            t.oncomplete = function () { resolve(r && r.result); };
            t.onerror = function () { reject(t.error); };
        });
    },

    // url = where the photo goes, fields = the form fields that go with it (session, item).
    async add(blob, url, fields) {
        this.rejected = null;
        try { await this.tx("readwrite", function (s) { return s.add({ url: url, fields: fields, blob: blob, at: Date.now() }); }); }
        catch (e) {
            // No IndexedDB (private window): send it straight away instead, still with retries.
            this.memory = this.memory || [];
            this.memory.push({ id: "m" + Date.now() + Math.random(), url: url, fields: fields, blob: blob });
        }
        this.pump();
    },

    async all() {
        var stored = [];
        try { stored = (await this.tx("readonly", function (s) { return s.getAll(); })) || []; } catch (e) { }
        return stored.concat(this.memory || []);
    },

    async remove(item) {
        if (typeof item.id === "string") { this.memory = (this.memory || []).filter(function (m) { return m.id !== item.id; }); return; }
        await this.tx("readwrite", function (s) { return s.delete(item.id); });
    },

    async save(item) {
        if (typeof item.id === "string") return;   // kept in memory: changed in place
        await this.tx("readwrite", function (s) { return s.put(item); });
    },

    // Photos whose session is gone (the server answered 409) wait here until a session is chosen - never thrown away
    // (2026-10-29: Vibeke's first photos were removed as "afvist" after their session was deleted).
    async stranded() { return (await this.all()).filter(function (i) { return i.stranded; }); },

    // "Send dem til denne session": the open session's fields go on every waiting photo, then they are sent.
    async adopt() {
        var open = document.querySelector("input[type=file][data-photo-url][data-fields]");
        var fields = open ? JSON.parse(open.dataset.fields || "{}") : null;
        if (!fields || !fields.sessionId) return;
        var items = await this.stranded();
        for (var i = 0; i < items.length; i++) { items[i].fields = fields; items[i].stranded = false; await this.save(items[i]); }
        this.pump();
    },

    async showStranded() {
        var waiting = (await this.stranded()).length;
        var canAdopt = !!document.querySelector("input[type=file][data-photo-url][data-fields]");
        document.querySelectorAll("[data-photo-stranded]").forEach(function (box) {
            box.hidden = waiting === 0;
            var text = box.querySelector("[data-photo-stranded-text]");
            if (text) text.textContent = "⚠ " + waiting + " billede" + (waiting === 1 ? "" : "r") + " venter på telefonen: den session, de blev taget i, findes ikke længere. "
                + (canAdopt ? "Tryk »Send dem til denne session«, så kommer de med her." : "Tryk på det, de er billeder af, så kan du sende dem dertil.");
            var btn = box.querySelector("[data-photo-adopt]");
            if (btn) btn.hidden = !canAdopt;
        });
    },

    async pump() {
        if (this.sending) return;
        this.sending = true;
        try {
            for (;;) {
                var items = (await this.all()).filter(function (i) { return !i.stranded; });
                this.show(items.length);
                await this.showStranded();
                if (items.length === 0) { this.failed = 0; return; }
                var item = items[0];
                var form = new FormData();
                form.append("foto", item.blob, "foto.jpg");
                Object.keys(item.fields || {}).forEach(function (k) { form.append(k, item.fields[k]); });
                var res = null;
                try { res = await fetch(item.url, { method: "POST", body: form, credentials: "same-origin" }); } catch (e) { }
                if (res && res.ok) {
                    await this.remove(item);
                    this.failed = 0;
                    this.tell();
                    continue;
                }
                if (res && res.status === 401) { this.show(items.length, "login"); return; }
                if (res && res.status === 409) {
                    // The photo is fine, its session is gone: keep it on the phone until a session is chosen.
                    item.stranded = true;
                    await this.save(item);
                    continue;
                }
                if (res && res.status >= 400 && res.status < 500 && res.status !== 408 && res.status !== 429) {
                    // The server read it and said no (not a picture etc.) - sending it again will not help.
                    this.rejected = await res.text().catch(function () { return ""; });
                    await this.remove(item);
                    continue;
                }
                this.failed++;
                this.show(items.length, "retry");
                var wait = Math.min(30000, 2000 * this.failed);
                await new Promise(function (r) { setTimeout(r, wait); });
            }
        } finally { this.sending = false; }
    },

    show(count, problem) {
        var text;
        if (problem === "login") text = "⚠ Du er logget ud. Log ind igen – de " + count + " billeder venter på telefonen og sendes bagefter.";
        else if (problem === "retry") text = "⚠ Ingen forbindelse lige nu. " + count + " billede" + (count === 1 ? "" : "r") + " venter på telefonen og sendes, så snart der er forbindelse.";
        else if (count > 0) text = "⏳ Sender " + count + " billede" + (count === 1 ? "" : "r") + " …";
        else text = this.rejected !== null ? "⚠ Et billede blev afvist: " + (this.rejected || "kunne ikke åbnes") + " Tag det igen." : "";
        var warn = !!problem || (count === 0 && this.rejected !== null);
        document.querySelectorAll("[data-photo-status]").forEach(function (el) {
            el.textContent = text;
            el.className = warn ? "lg-warn" : (count > 0 ? "lg-answer" : "");
            el.hidden = text === "";
        });
    },

    tell() {
        this.listeners.forEach(function (l) { l.invokeMethodAsync("PhotoSent").catch(function () { }); });
    },

    listen(dotnetRef) { this.listeners.push(dotnetRef); this.pump(); },
    unlisten(dotnetRef) { this.listeners = this.listeners.filter(function (l) { return l !== dotnetRef; }); }
};

// Plain JS handlers, so picking and sending photos works even while the Blazor connection is away.
document.addEventListener("change", function (e) {
    var input = e.target;
    if (!input.matches || !input.matches("input[type=file][data-photo-url]")) return;
    var fields = JSON.parse(input.dataset.fields || "{}"), url = input.dataset.photoUrl;
    var files = Array.prototype.slice.call(input.files || []);
    input.value = "";
    // iPhone Safari (2026-10-31, Martin's gallery upload: "Intet billede"): a picked File put in IndexedDB as it is can
    // come back empty later. Copy its bytes into a plain Blob first - one at a time, so 100 photos do not fill memory.
    (async function () {
        for (var i = 0; i < files.length; i++) {
            try {
                var copy = new Blob([await files[i].arrayBuffer()], { type: files[i].type || "image/jpeg" });
                if (copy.size > 0) await photoQueue.add(copy, url, fields);
            } catch (err) { }
        }
    })();
});
document.addEventListener("click", function (e) {
    var t = e.target.closest ? e.target.closest("[data-photo-adopt]") : null;
    if (!t) return;
    e.preventDefault();
    photoQueue.adopt();
});
// Photos left from last time (page closed, no network) go as soon as the page is open again.
window.addEventListener("online", function () { photoQueue.pump(); });
if (document.readyState !== "loading") photoQueue.pump(); else document.addEventListener("DOMContentLoaded", function () { photoQueue.pump(); });

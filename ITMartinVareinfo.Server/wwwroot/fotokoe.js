// Photo queue (2026-10-19). Photos used to travel over the Blazor connection, and phones drop that connection while
// the camera app is open - the page reloaded and the photo was gone without a word. Now every photo is first kept in
// the phone's own storage (IndexedDB), then sent with a plain upload, retried until the server has it - also after a
// reload. The in-page camera takes photo after photo without leaving the page at all.
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

    // url = where the photo goes, fields = the form fields that go with it (box, item).
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

    async pump() {
        if (this.sending) return;
        this.sending = true;
        try {
            for (;;) {
                var items = await this.all();
                this.show(items.length);
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
    unlisten(dotnetRef) { this.listeners = this.listeners.filter(function (l) { return l !== dotnetRef; }); },

    // ---- in-page camera: no camera app, so the page is never left ----
    cam: { stream: null, capture: null, taken: 0, url: null, fields: null },

    async openCamera(button) {
        var panel = document.getElementById("foto-cam"), video = document.getElementById("foto-video");
        var note = document.getElementById("foto-cam-note");
        this.cam.url = button.dataset.photoUrl;
        this.cam.fields = JSON.parse(button.dataset.fields || "{}");
        this.cam.taken = 0;
        note.textContent = "";
        var sets = [
            { video: { facingMode: { ideal: "environment" }, width: { ideal: 4096 }, height: { ideal: 3072 } }, audio: false },
            { video: { facingMode: "environment" }, audio: false },
            { video: true, audio: false }
        ];
        for (var i = 0; i < sets.length && !this.cam.stream; i++) {
            try { this.cam.stream = await navigator.mediaDevices.getUserMedia(sets[i]); } catch (e) { }
        }
        if (!this.cam.stream) {
            note.textContent = "⚠ Kameraet kunne ikke startes her. Brug knappen »Brug telefonens kamera« nedenfor.";
            panel.hidden = false;
            return;
        }
        var track = this.cam.stream.getVideoTracks()[0];
        this.cam.capture = "ImageCapture" in window ? new ImageCapture(track) : null;
        try {
            var caps = track.getCapabilities ? track.getCapabilities() : null;
            if (caps && caps.focusMode && caps.focusMode.indexOf("continuous") >= 0) await track.applyConstraints({ advanced: [{ focusMode: "continuous" }] });
        } catch (e) { }
        video.srcObject = this.cam.stream;
        panel.hidden = false;
        button.hidden = true;
        await video.play().catch(function () { });
    },

    async shoot() {
        var video = document.getElementById("foto-video"), note = document.getElementById("foto-cam-note");
        if (!this.cam.stream) return;
        var blob = null;
        if (this.cam.capture) { try { blob = await this.cam.capture.takePhoto(); } catch (e) { blob = null; } }
        if (!blob && video.videoWidth > 0) {
            var c = document.createElement("canvas");
            c.width = video.videoWidth; c.height = video.videoHeight;
            c.getContext("2d").drawImage(video, 0, 0);
            blob = await new Promise(function (r) { c.toBlob(r, "image/jpeg", 0.92); });
        }
        if (!blob) { note.textContent = "⚠ Billedet blev ikke taget – prøv igen."; return; }
        window.scanner && window.scanner.beep && window.scanner.beep();
        video.classList.remove("is-flash"); void video.offsetWidth; video.classList.add("is-flash");
        this.cam.taken++;
        note.textContent = "✓ " + this.cam.taken + " billede" + (this.cam.taken === 1 ? "" : "r") + " taget. Læg de næste ting ud og tag det næste.";
        await this.add(blob, this.cam.url, this.cam.fields);
    },

    closeCamera() {
        if (this.cam.stream) this.cam.stream.getTracks().forEach(function (t) { t.stop(); });
        this.cam.stream = null; this.cam.capture = null;
        var panel = document.getElementById("foto-cam"), video = document.getElementById("foto-video");
        if (video) video.srcObject = null;
        if (panel) panel.hidden = true;
        document.querySelectorAll("[data-camera-open]").forEach(function (b) { b.hidden = false; });
    }
};

// Plain JS handlers, so taking and sending photos works even while the Blazor connection is away.
document.addEventListener("change", function (e) {
    var input = e.target;
    if (!input.matches || !input.matches("input[type=file][data-photo-url]")) return;
    var fields = JSON.parse(input.dataset.fields || "{}");
    Array.prototype.slice.call(input.files || []).forEach(function (f) { photoQueue.add(f, input.dataset.photoUrl, fields); });
    input.value = "";
});
document.addEventListener("click", function (e) {
    var t = e.target.closest ? e.target.closest("[data-camera-open],[data-camera-shoot],[data-camera-close]") : null;
    if (!t) return;
    e.preventDefault();
    if (t.hasAttribute("data-camera-open")) photoQueue.openCamera(t);
    else if (t.hasAttribute("data-camera-shoot")) photoQueue.shoot();
    else photoQueue.closeCamera();
});
// Photos left from last time (page closed, no network) go as soon as the page is open again.
window.addEventListener("online", function () { photoQueue.pump(); });
if (document.readyState !== "loading") photoQueue.pump(); else document.addEventListener("DOMContentLoaded", function () { photoQueue.pump(); });

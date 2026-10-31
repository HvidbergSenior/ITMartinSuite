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
        var open = document.querySelector("[data-camera-open][data-fields]");
        var fields = open ? JSON.parse(open.dataset.fields || "{}") : null;
        if (!fields || !fields.sessionId) return;
        var items = await this.stranded();
        for (var i = 0; i < items.length; i++) { items[i].fields = fields; items[i].stranded = false; await this.save(items[i]); }
        this.pump();
    },

    async showStranded() {
        var waiting = (await this.stranded()).length;
        var canAdopt = !!document.querySelector("[data-camera-open][data-fields]");
        document.querySelectorAll("[data-photo-stranded]").forEach(function (box) {
            box.hidden = waiting === 0;
            var text = box.querySelector("[data-photo-stranded-text]");
            if (text) text.textContent = "⚠ " + waiting + " billede" + (waiting === 1 ? "" : "r") + " venter på telefonen: den session, de blev taget i, findes ikke længere. "
                + (canAdopt ? "Tryk »Send dem til denne session«, så kommer de med her." : "Vælg en session ovenfor, så kan du sende dem dertil.");
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
        var why = "";
        for (var i = 0; i < sets.length && !this.cam.stream; i++) {
            try { this.cam.stream = await navigator.mediaDevices.getUserMedia(sets[i]); } catch (e) { why = (e && e.name) || why; }
        }
        if (!this.cam.stream) {
            note.textContent = this.cameraProblem(why);
            panel.hidden = false;
            return;
        }
        var track = this.cam.stream.getVideoTracks()[0];
        this.cam.capture = "ImageCapture" in window ? new ImageCapture(track) : null;
        try {
            var caps = track.getCapabilities ? track.getCapabilities() : null;
            if (caps && caps.focusMode && caps.focusMode.indexOf("continuous") >= 0) await track.applyConstraints({ advanced: [{ focusMode: "continuous" }] });
        } catch (e) { }
        panel.hidden = false;
        button.hidden = true;
        await this.startVideo(video, this.cam.stream);
        await this.tools(track);
    },

    // Zoom, lens and light - only what this phone's browser offers (2026-10-21: "should it be zoomable?").
    // iPhone Safari has no zoom on a web camera but lists its lenses; Android Chrome has zoom and torch.
    async tools(track) {
        var box = document.getElementById("foto-cam-tools"), self = this;
        if (!box) return;
        box.innerHTML = "";
        var caps = track.getCapabilities ? track.getCapabilities() : {};
        if (caps.zoom && caps.zoom.max > caps.zoom.min) {
            var label = document.createElement("label"); label.className = "lg-cam-zoom"; label.textContent = "Zoom ";
            var range = document.createElement("input");
            range.type = "range"; range.min = caps.zoom.min; range.max = Math.min(caps.zoom.max, 5); range.step = caps.zoom.step || 0.1;
            range.value = (track.getSettings && track.getSettings().zoom) || caps.zoom.min;
            range.addEventListener("input", function () { track.applyConstraints({ advanced: [{ zoom: Number(range.value) }] }).catch(function () { }); });
            label.appendChild(range); box.appendChild(label);
        }
        if (caps.torch) {
            var on = false, light = document.createElement("button");
            light.type = "button"; light.className = "k-btn k-btn-secondary k-btn-sm"; light.textContent = "💡 Lys til";
            light.addEventListener("click", function () {
                on = !on; light.textContent = on ? "💡 Lys fra" : "💡 Lys til";
                track.applyConstraints({ advanced: [{ torch: on }] }).catch(function () { });
            });
            box.appendChild(light);
        }
        var lenses = [];
        try {
            lenses = (await navigator.mediaDevices.enumerateDevices())
                .filter(function (d) { return d.kind === "videoinput" && /back|rear|bag|environment/i.test(d.label); });
        } catch (e) { }
        if (lenses.length > 1) {
            var pick = document.createElement("select"); pick.className = "k-input lg-cam-lens"; pick.setAttribute("aria-label", "Linse");
            var current = track.getSettings ? track.getSettings().deviceId : "";
            lenses.forEach(function (d) {
                var o = document.createElement("option"); o.value = d.deviceId; o.textContent = self.lensName(d.label);
                if (d.deviceId === current) o.selected = true;
                pick.appendChild(o);
            });
            pick.addEventListener("change", function () { self.switchLens(pick.value); });
            box.appendChild(pick);
        }
    },

    lensName(label) {
        if (/ultra/i.test(label)) return "0,5× vidvinkel (meget med)";
        if (/tele/i.test(label)) return "Tele 2-3× (tæt på)";
        if (/dual|triple/i.test(label)) return "Auto (skifter selv)";
        return "1× normal";
    },

    async switchLens(deviceId) {
        var video = document.getElementById("foto-video");
        if (this.cam.stream) this.cam.stream.getTracks().forEach(function (t) { t.stop(); });
        try {
            this.cam.stream = await navigator.mediaDevices.getUserMedia({ video: { deviceId: { exact: deviceId }, width: { ideal: 4096 }, height: { ideal: 3072 } }, audio: false });
        } catch (e) { this.cam.stream = await navigator.mediaDevices.getUserMedia({ video: { deviceId: { exact: deviceId } }, audio: false }); }
        var track = this.cam.stream.getVideoTracks()[0];
        this.cam.capture = "ImageCapture" in window ? new ImageCapture(track) : null;
        await this.startVideo(video, this.cam.stream);
        await this.tools(track);
    },

    // iPhone Safari (2026-10-25: a helper saw no picture): Blazor sets "muted" only as an attribute, not the
    // muted property, and the tap is used up by the getUserMedia wait - so Safari refuses play() and the box stays black.
    // Set the properties ourselves; if Safari still refuses, one tap on the box starts it.
    async startVideo(video, stream) {
        var note = document.getElementById("foto-cam-note");
        video.muted = true; video.defaultMuted = true; video.autoplay = true; video.playsInline = true;
        video.setAttribute("muted", ""); video.setAttribute("playsinline", ""); video.setAttribute("webkit-playsinline", "");
        video.srcObject = stream;
        try { await video.play(); }
        catch (e) {
            if (note) note.textContent = "Tryk på det sorte felt for at se kameraet.";
            video.addEventListener("click", function () { video.play().then(function () { if (note) note.textContent = ""; }).catch(function () { }); }, { once: true });
        }
    },

    async shoot() {
        var video = document.getElementById("foto-video"), note = document.getElementById("foto-cam-note");
        if (!this.cam.stream) return;
        var blob = null;
        if (this.cam.capture) { try { blob = await this.cam.capture.takePhoto(); } catch (e) { blob = null; } }
        if (!blob) blob = await this.grab(video);
        if (!blob) { this.useOwnCamera(note); return; }
        // How big did it come out? Small = the AI cannot read titles: say so at once.
        var size = "";
        var small = false;
        try { var bmp = await createImageBitmap(blob); size = bmp.width + "×" + bmp.height; small = Math.max(bmp.width, bmp.height) < 2000; if (bmp.close) bmp.close(); } catch (e) { }
        window.scanner && window.scanner.beep && window.scanner.beep();
        video.classList.remove("is-flash"); void video.offsetWidth; video.classList.add("is-flash");
        this.cam.taken++;
        note.textContent = small
            ? "⚠ Billedet blev lille (" + size + ") – titlerne kan måske ikke læses. Gå tættere på, eller brug »Brug telefonens kamera« nedenfor."
            : "✓ " + this.cam.taken + " billede" + (this.cam.taken === 1 ? "" : "r") + " taget" + (size ? " (" + size + ")" : "") + ". Læg de næste ting ud og tag det næste.";
        await this.add(blob, this.cam.url, this.cam.fields);
    },

    // A still from the video (iPhone Safari has no ImageCapture). 2026-10-31: Vibeke's iPhone said "Billedet blev ikke
    // taget" every time in her øvesession - the video had no frame yet (play refused, or iOS stopped the camera while
    // the app was in the background), and a full-size canvas can come back empty on iPhone. So: start the camera again
    // if it was stopped, wait up to 2 s for a frame, and keep the still at most 4096 px.
    async grab(video) {
        var track = this.cam.stream && this.cam.stream.getVideoTracks()[0];
        if (track && track.readyState === "ended" && track.getSettings) {
            try { await this.switchLens(track.getSettings().deviceId); } catch (e) { }
        }
        if (video.paused) { try { await video.play(); } catch (e) { } }
        for (var waited = 0; video.videoWidth === 0 && waited < 2000; waited += 100)
            await new Promise(function (r) { setTimeout(r, 100); });
        if (video.videoWidth === 0) return null;
        var sides = [4096, 2048];
        for (var i = 0; i < sides.length; i++) {
            var scale = Math.min(1, sides[i] / Math.max(video.videoWidth, video.videoHeight));
            var c = document.createElement("canvas");
            c.width = Math.round(video.videoWidth * scale); c.height = Math.round(video.videoHeight * scale);
            c.getContext("2d").drawImage(video, 0, 0, c.width, c.height);
            var blob = await new Promise(function (r) { c.toBlob(r, "image/jpeg", 0.92); });
            c.width = c.height = 0;   // iPhone keeps canvas memory until told
            if (blob) return blob;
        }
        return null;
    },

    // The in-page camera gave no picture: say so, and put the phone's own camera right under the words.
    useOwnCamera(note) {
        note.textContent = "⚠ Kameraet inde i siden gav ikke et billede på denne telefon. Brug telefonens eget kamera i stedet – billederne kommer med i samme session. ";
        var own = document.querySelector("label.lg-camera input[capture]");
        if (!own) return;
        var label = document.createElement("label");
        label.className = "k-btn k-btn-primary lg-big";
        label.textContent = "Brug telefonens kamera";
        var input = own.cloneNode(true);
        label.appendChild(input);
        note.appendChild(label);
    },

    // Why the in-page camera did not start, in words a helper can act on (2026-10-29: Vibeke, iPhone, Vareinfo as a
    // home-screen icon - such an app asks for the camera again every time it is opened, and a "Tillad ikke" sticks
    // until the app is closed all the way). The phone's own camera (the button below) works in every case.
    cameraProblem(name) {
        var ua = navigator.userAgent || "";
        var iphone = /iPhone|iPad|iPod/.test(ua);
        var homeScreen = (window.navigator.standalone === true) || (window.matchMedia && window.matchMedia("(display-mode: standalone)").matches);
        var inApp = /FBAN|FBAV|FB_IAB|Instagram|Messenger/.test(ua);
        var fallback = " Eller brug »Brug telefonens kamera« nedenfor – det virker altid.";
        if (inApp) return "⚠ Siden er åbnet inde i Facebook/Messenger, og der må den ikke bruge kameraet. Tryk ⋯ og vælg »Åbn i browser«." + fallback;
        if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia)
            return "⚠ Telefonens browser kan ikke bruge kameraet inde i siden" + (iphone ? " (iPhone skal have iOS 13.4 eller nyere)." : ".") + fallback;
        if (name === "NotAllowedError" || name === "SecurityError")
            return iphone && homeScreen
                ? "⚠ Kameraet fik ikke lov. Luk Vareinfo helt (stryg den væk), åbn den igen, og tryk »Tillad«, når telefonen spørger om kameraet." + fallback
                : iphone
                    ? "⚠ Kameraet fik ikke lov. Gå til Indstillinger › Safari › Kamera og vælg »Spørg« eller »Tillad«, og prøv igen." + fallback
                    : "⚠ Kameraet fik ikke lov. Tryk på hængelåsen ved adressen › Tilladelser › Kamera › Tillad, og prøv igen." + fallback;
        if (name === "NotReadableError" || name === "AbortError")
            return "⚠ Kameraet bruges af en anden app lige nu. Luk den (fx kameraet eller et videoopkald), og prøv igen." + fallback;
        if (name === "NotFoundError" || name === "OverconstrainedError")
            return "⚠ Telefonen fandt ikke et kamera, siden kan bruge." + fallback;
        return "⚠ Kameraet kunne ikke startes her." + fallback;
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
    var t = e.target.closest ? e.target.closest("[data-camera-open],[data-camera-shoot],[data-camera-close],[data-photo-adopt]") : null;
    if (!t) return;
    e.preventDefault();
    if (t.hasAttribute("data-photo-adopt")) photoQueue.adopt();
    else if (t.hasAttribute("data-camera-open")) photoQueue.openCamera(t);
    else if (t.hasAttribute("data-camera-shoot")) photoQueue.shoot();
    else {
        photoQueue.closeCamera();
        // Helpers: done = back to the session list. Photos not yet sent wait in IndexedDB and go after the reload.
        if (t.dataset.doneUrl) location.href = t.dataset.doneUrl;
    }
});
// Photos left from last time (page closed, no network) go as soon as the page is open again.
window.addEventListener("online", function () { photoQueue.pump(); });
if (document.readyState !== "loading") photoQueue.pump(); else document.addEventListener("DOMContentLoaded", function () { photoQueue.pump(); });

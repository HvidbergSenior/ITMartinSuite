// Browser-side checks for Tjek. Everything here runs on the visitor's own device,
// nothing is installed. Each check returns {id, name, status: ok|warn|bad|info, value, hint}.
// Audio/camera tests need a user gesture, so they are separate functions called from buttons.
window.tjek = (function () {
    const errors = [];
    window.addEventListener("error", e => errors.push((e.message || "fejl") + (e.filename ? " @ " + e.filename.split("/").pop() + ":" + e.lineno : "")));
    window.addEventListener("unhandledrejection", e => errors.push("promise: " + (e.reason && e.reason.message ? e.reason.message : String(e.reason))));

    const r = (id, name, status, value, hint) => ({ id, name, status, value: value == null ? "" : String(value), hint: hint || "" });

    function osName() {
        const ua = navigator.userAgent, d = navigator.userAgentData;
        if (d && d.platform) return d.platform + (d.mobile ? " (mobil)" : "");
        if (/iPhone|iPad|iPod/.test(ua)) return "iOS";
        if (/Android/.test(ua)) return "Android";
        if (/Windows/.test(ua)) return "Windows";
        if (/Mac OS/.test(ua)) return "macOS";
        if (/Linux/.test(ua)) return "Linux";
        return "Ukendt";
    }
    function browserName() {
        const d = navigator.userAgentData;
        if (d && d.brands) { const b = d.brands.find(x => !/Not|Chromium/.test(x.brand)) || d.brands[0]; if (b) return b.brand + " " + b.version; }
        const ua = navigator.userAgent, m = ua.match(/(Firefox|Edg|OPR|Chrome|Safari|CriOS|FxiOS)\/([\d.]+)/);
        return m ? m[1] + " " + m[2].split(".")[0] : "Ukendt";
    }

    async function latency(url, n) {
        const t = [];
        for (let i = 0; i < n; i++) {
            const a = performance.now();
            try { await fetch(url + "?t=" + Date.now() + i, { cache: "no-store" }); t.push(performance.now() - a); } catch { }
        }
        if (!t.length) return null;
        t.sort((x, y) => x - y);
        return { min: Math.round(t[0]), median: Math.round(t[Math.floor(t.length / 2)]), max: Math.round(t[t.length - 1]) };
    }
    async function download(url) {
        const a = performance.now();
        try {
            const res = await fetch(url + "?t=" + Date.now(), { cache: "no-store" });
            const buf = await res.arrayBuffer();
            const s = (performance.now() - a) / 1000;
            return { mbit: Math.round(buf.byteLength * 8 / s / 1e6 * 10) / 10, bytes: buf.byteLength };
        } catch { return null; }
    }

    async function run() {
        const out = [];
        // --- enhed ---
        out.push(r("os", "Styresystem", "info", osName()));
        out.push(r("browser", "Browser", "info", browserName()));
        const cores = navigator.hardwareConcurrency, mem = navigator.deviceMemory;
        if (cores) out.push(r("cpu", "CPU-kerner", cores >= 4 ? "ok" : "warn", cores, cores < 4 ? "Få kerner - tunge sider og videomøder kan hakke." : ""));
        if (mem) out.push(r("ram", "Hukommelse (browser-anslået)", mem >= 4 ? "ok" : "warn", mem + " GB", mem < 4 ? "Under 4 GB - luk andre faner/programmer ved problemer." : ""));
        const dpr = window.devicePixelRatio || 1;
        out.push(r("screen", "Skærm", "info", screen.width + "×" + screen.height + " @" + dpr + "x, vindue " + innerWidth + "×" + innerHeight));
        out.push(r("touch", "Berøring", "info", navigator.maxTouchPoints > 0 ? "Ja (" + navigator.maxTouchPoints + " punkter)" : "Nej"));
        out.push(r("lang", "Sprog / tidszone", "info", navigator.language + " / " + Intl.DateTimeFormat().resolvedOptions().timeZone));
        out.push(r("online", "Online", navigator.onLine ? "ok" : "bad", navigator.onLine ? "Ja" : "Nej", navigator.onLine ? "" : "Browseren melder offline."));
        const c = navigator.connection;
        if (c) out.push(r("net", "Netværkstype", c.saveData ? "warn" : "info", (c.effectiveType || "?") + (c.type ? " / " + c.type : "") + (c.downlink ? ", ~" + c.downlink + " Mbit/s" : "") + (c.rtt ? ", rtt " + c.rtt + " ms" : ""), c.saveData ? "Datasparer er slået til - billeder/video kan blive nedskaleret." : ""));
        // --- battery ---
        try {
            if (navigator.getBattery) { const b = await navigator.getBattery(); const p = Math.round(b.level * 100); out.push(r("battery", "Batteri", b.charging || p > 20 ? "ok" : "warn", p + " %" + (b.charging ? ", oplader" : ""), !b.charging && p <= 20 ? "Lavt batteri - mange enheder skruer ned for ydelsen." : "")); }
        } catch { }
        // --- storage ---
        try {
            if (navigator.storage && navigator.storage.estimate) { const e = await navigator.storage.estimate(); const q = e.quota ? Math.round(e.quota / 1e9) : 0; out.push(r("storage", "Browserplads til rådighed", q >= 2 ? "ok" : "warn", q + " GB (" + Math.round((e.usage || 0) / 1e6) + " MB brugt)", q < 2 ? "Lidt ledig plads - enheden kan være næsten fuld." : "")); }
        } catch { }
        // --- latency + download ---
        const lat = await latency("/api/ping", 5);
        if (lat) out.push(r("latency", "Svartid til serveren", lat.median < 80 ? "ok" : lat.median < 200 ? "warn" : "bad", lat.median + " ms (min " + lat.min + ", max " + lat.max + ")", lat.median >= 200 ? "Høj svartid - dårligt Wi-Fi eller mobilnet? Prøv tættere på routeren eller kabel." : lat.median >= 80 ? "Lidt langsomt, men brugbart." : ""));
        else out.push(r("latency", "Svartid til serveren", "bad", "Ingen svar", "Kan ikke nå serveren."));
        const dl = await download("/api/blob");
        if (dl) out.push(r("download", "Downloadhastighed (kort test)", dl.mbit >= 20 ? "ok" : dl.mbit >= 5 ? "warn" : "bad", dl.mbit + " Mbit/s", dl.mbit < 5 ? "Meget langsomt - video og store sider vil hakke." : dl.mbit < 20 ? "OK til det meste, HD-video kan buffere." : ""));
        // --- websocket (Blazor needs it) ---
        try {
            const ok = await new Promise(res => { const ws = new WebSocket((location.protocol === "https:" ? "wss://" : "ws://") + location.host + "/_blazor"); const t = setTimeout(() => { try { ws.close(); } catch { } res(false); }, 4000); ws.onopen = () => { clearTimeout(t); ws.close(); res(true); }; ws.onerror = () => { clearTimeout(t); res(false); }; });
            out.push(r("ws", "WebSocket", ok ? "ok" : "warn", ok ? "Virker" : "Blokeret?", ok ? "" : "Nogle firmanetværk/proxyer blokerer WebSocket - apps kan falde tilbage til langsommere forbindelse."));
        } catch { }
        // --- WebRTC ---
        out.push(r("webrtc", "WebRTC (videomøder)", window.RTCPeerConnection ? "ok" : "bad", window.RTCPeerConnection ? "Understøttet" : "Mangler", window.RTCPeerConnection ? "" : "Browseren kan ikke lave videoopkald."));
        // --- media devices ---
        try {
            if (navigator.mediaDevices && navigator.mediaDevices.enumerateDevices) {
                const devs = await navigator.mediaDevices.enumerateDevices();
                const mics = devs.filter(d => d.kind === "audioinput"), outs = devs.filter(d => d.kind === "audiooutput"), cams = devs.filter(d => d.kind === "videoinput");
                const named = devs.some(d => d.label);
                out.push(r("mics", "Mikrofoner", mics.length ? "ok" : "warn", mics.length + (named ? ": " + mics.map(d => d.label).join(", ") : " (navne vises efter mikrofontest)"), mics.length ? "" : "Ingen mikrofon fundet."));
                out.push(r("outs", "Højttalere / hovedtelefoner", outs.length ? "ok" : "info", outs.length ? outs.length + (named ? ": " + outs.map(d => d.label).join(", ") : "") : "Browseren viser ikke udgange (normalt på iPhone/Safari)"));
                out.push(r("cams", "Kameraer", cams.length ? "ok" : "warn", cams.length + (named ? ": " + cams.map(d => d.label).join(", ") : ""), cams.length ? "" : "Intet kamera fundet."));
            } else out.push(r("media", "Lyd/kamera-adgang", "bad", "Ikke tilgængelig", "Siden skal åbnes via https for at bruge mikrofon/kamera."));
        } catch (e) { out.push(r("media", "Lyd/kamera-adgang", "warn", "Fejl: " + e.message)); }
        // --- permissions ---
        try {
            if (navigator.permissions) {
                for (const p of ["microphone", "camera", "notifications"]) {
                    try { const s = await navigator.permissions.query({ name: p }); out.push(r("perm-" + p, "Tilladelse: " + ({ microphone: "mikrofon", camera: "kamera", notifications: "notifikationer" })[p], s.state === "denied" ? "warn" : "info", ({ granted: "Givet", denied: "Afvist", prompt: "Ikke spurgt endnu" })[s.state] || s.state, s.state === "denied" ? "Afvist i browseren - slå til under sidens indstillinger (låse-ikonet i adresselinjen)." : "")); } catch { }
                }
            }
        } catch { }
        // --- PWA / notifications ---
        out.push(r("sw", "Installérbar app (PWA)", "serviceWorker" in navigator ? "ok" : "info", "serviceWorker" in navigator ? "Ja" : "Nej"));
        out.push(r("cookies", "Cookies", navigator.cookieEnabled ? "ok" : "bad", navigator.cookieEnabled ? "Slået til" : "Slået fra", navigator.cookieEnabled ? "" : "Login virker ikke uden cookies."));
        try { localStorage.setItem("tjek", "1"); localStorage.removeItem("tjek"); out.push(r("ls", "Lokal lagring", "ok", "Virker")); } catch { out.push(r("ls", "Lokal lagring", "warn", "Blokeret", "Privat vindue eller blokerede sitedata - apps kan ikke huske indstillinger.")); }
        if (errors.length) out.push(r("jserr", "JavaScript-fejl på siden", "warn", errors.length + " fejl: " + errors.slice(0, 3).join(" | ")));
        else out.push(r("jserr", "JavaScript-fejl på siden", "ok", "Ingen"));
        return out;
    }

    // ---- microphone level meter: resolves after `seconds` with peak/avg in % ----
    let micStream = null;
    async function micTest(seconds, dotnet) {
        const Ctx = window.AudioContext || window.webkitAudioContext;
        if (!navigator.mediaDevices || !Ctx) return { ok: false, error: "Mikrofon understøttes ikke i denne browser" };
        try {
            micStream = await navigator.mediaDevices.getUserMedia({ audio: true });
            const ctx = new Ctx(), src = ctx.createMediaStreamSource(micStream), an = ctx.createAnalyser();
            an.fftSize = 1024; src.connect(an);
            const buf = new Uint8Array(an.fftSize);
            let peak = 0, sum = 0, n = 0;
            const track = micStream.getAudioTracks()[0], label = track ? track.label : "";
            const settings = track && track.getSettings ? track.getSettings() : {};
            const end = performance.now() + seconds * 1000;
            while (performance.now() < end) {
                an.getByteTimeDomainData(buf);
                let m = 0; for (let i = 0; i < buf.length; i++) { const v = Math.abs(buf[i] - 128) / 128; if (v > m) m = v; }
                peak = Math.max(peak, m); sum += m; n++;
                if (dotnet) { try { await dotnet.invokeMethodAsync("MicLevel", Math.round(m * 100)); } catch { } }
                await new Promise(res => setTimeout(res, 100));
            }
            micStream.getTracks().forEach(t => t.stop()); micStream = null; ctx.close();
            return { ok: true, label, peak: Math.round(peak * 100), avg: Math.round(sum / n * 100), sampleRate: ctx.sampleRate, echoCancellation: settings.echoCancellation, noiseSuppression: settings.noiseSuppression };
        } catch (e) { return { ok: false, error: e.name === "NotAllowedError" ? "Adgang til mikrofonen blev afvist" : e.message }; }
    }

    // ---- test tone (1 s, 440 Hz) to the chosen output (setSinkId where supported) ----
    async function tone(deviceId) {
        const Ctx = window.AudioContext || window.webkitAudioContext;
        if (!Ctx) return { ok: false, error: "Ingen lyd-API" };
        try {
            const ctx = new Ctx();
            if (deviceId && ctx.setSinkId) { try { await ctx.setSinkId(deviceId); } catch (e) { return { ok: false, error: "Kunne ikke vælge udgang: " + e.message }; } }
            const o = ctx.createOscillator(), g = ctx.createGain();
            o.frequency.value = 440; g.gain.value = 0.2; o.connect(g); g.connect(ctx.destination);
            o.start(); await new Promise(res => setTimeout(res, 1000)); o.stop(); await ctx.close();
            return { ok: true, sinkSupported: !!ctx.setSinkId };
        } catch (e) { return { ok: false, error: e.message }; }
    }

    async function outputs() {
        try { const d = await navigator.mediaDevices.enumerateDevices(); return d.filter(x => x.kind === "audiooutput").map(x => ({ id: x.deviceId, label: x.label || "Udgang" })); } catch { return []; }
    }

    // ---- camera: opens, reads resolution, closes ----
    async function camTest() {
        if (!navigator.mediaDevices) return { ok: false, error: "Kamera understøttes ikke" };
        try {
            const s = await navigator.mediaDevices.getUserMedia({ video: true });
            const t = s.getVideoTracks()[0], st = t.getSettings ? t.getSettings() : {};
            const label = t.label; s.getTracks().forEach(x => x.stop());
            return { ok: true, label, width: st.width, height: st.height, fps: st.frameRate };
        } catch (e) { return { ok: false, error: e.name === "NotAllowedError" ? "Adgang til kameraet blev afvist" : e.message }; }
    }

    function deviceName() { try { return localStorage.getItem("tjek-device") || ""; } catch { return ""; } }
    function setDeviceName(n) { try { localStorage.setItem("tjek-device", n); } catch { } }

    // A secret code per browser (no 0/O/1/I so it can be typed from the screen). Results are saved under it.
    function ownerKey() {
        const abc = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        try {
            let k = localStorage.getItem("tjek-key");
            if (!k) { const b = crypto.getRandomValues(new Uint8Array(10)); k = Array.from(b, x => abc[x % abc.length]).join(""); localStorage.setItem("tjek-key", k); }
            return k;
        } catch { return ""; }
    }

    return { run, micTest, tone, outputs, camTest, deviceName, setDeviceName, ownerKey };
})();

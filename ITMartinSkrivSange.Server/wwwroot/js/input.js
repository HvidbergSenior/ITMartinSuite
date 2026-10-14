// SkrivSange - which input to record from (user 2026-10-14: "guitar in the HyperX, headphones in the PC").
// The browser's default is often the built-in or headset mic, so the choice is remembered per device in localStorage,
// and studio.js records from it. A level meter shows the guitar is coming in before you record.
window.skrivInput = (function () {
    var KEY = "skrivsange_input";
    var meter = null; // { stream, ctx, raf, timeout }

    function chosen() { try { return localStorage.getItem(KEY) || ""; } catch (e) { return ""; } }

    // The constraints studio.js uses: no voice-call processing, the chosen device, and mono so a guitar on input 1 of an
    // interface is on both sides of the take instead of only the left.
    function constraints() {
        var c = { echoCancellation: false, noiseSuppression: false, autoGainControl: false, channelCount: { ideal: 1 } };
        var id = chosen();
        if (id) c.deviceId = { exact: id };
        return c;
    }

    // Names are only shown after the page has been allowed the microphone once, so ask first.
    async function list() {
        if (!window.isSecureContext || !navigator.mediaDevices) return { ok: false, inputs: [], chosen: "" };
        var s = await navigator.mediaDevices.getUserMedia({ audio: true });
        s.getTracks().forEach(function (t) { t.stop(); });
        var all = await navigator.mediaDevices.enumerateDevices();
        var inputs = all.filter(function (d) { return d.kind === "audioinput" && d.deviceId !== "communications"; })
            .map(function (d, i) { return { id: d.deviceId, label: d.label || ("Indgang " + (i + 1)) }; });
        return { ok: true, inputs: inputs, chosen: chosen() };
    }

    function choose(id) {
        try { if (id) localStorage.setItem(KEY, id); else localStorage.removeItem(KEY); } catch (e) { }
    }

    // Shows the level in the element with id barId for 20 seconds (or until stopMeter).
    async function startMeter(barId) {
        stopMeter();
        var stream;
        try { stream = await navigator.mediaDevices.getUserMedia({ audio: constraints() }); }
        catch (e) {
            // the remembered device is gone (unplugged) - fall back to the default
            choose("");
            stream = await navigator.mediaDevices.getUserMedia({ audio: constraints() });
        }
        var ctx = new (window.AudioContext || window.webkitAudioContext)();
        var an = ctx.createAnalyser();
        an.fftSize = 1024;
        ctx.createMediaStreamSource(stream).connect(an);
        var data = new Float32Array(an.fftSize);
        var bar = document.getElementById(barId);
        meter = { stream: stream, ctx: ctx, raf: 0, timeout: setTimeout(stopMeter, 20000) };
        (function tick() {
            an.getFloatTimeDomainData(data);
            var peak = 0;
            for (var i = 0; i < data.length; i++) peak = Math.max(peak, Math.abs(data[i]));
            var db = peak > 0 ? 20 * Math.log10(peak) : -100;      // -60 dB = empty bar, 0 dB = full
            var pct = Math.max(0, Math.min(100, (db + 60) / 60 * 100));
            if (bar) { bar.style.width = pct + "%"; bar.setAttribute("data-level", db > -3 ? "hot" : db > -40 ? "ok" : "low"); }
            if (meter) meter.raf = requestAnimationFrame(tick);
        })();
    }

    function stopMeter() {
        if (!meter) return;
        cancelAnimationFrame(meter.raf);
        clearTimeout(meter.timeout);
        meter.stream.getTracks().forEach(function (t) { t.stop(); });
        try { meter.ctx.close(); } catch (e) { }
        meter = null;
    }

    return { list: list, choose: choose, constraints: constraints, startMeter: startMeter, stopMeter: stopMeter };
})();

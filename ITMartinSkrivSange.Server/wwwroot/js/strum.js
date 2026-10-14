// SkrivSange - plays chord progressions as a simple guitar strum in the browser (plan 2026-10-14: Web Audio, no sound files).
// Each string is a plucked-string sound (Karplus-Strong) made once per pitch and reused. One chord = one bar of 8 eighths.
window.skrivStrum = (function () {
    // Frets per string, low E to high e. -1 = not played. Same open grips as Chords.OpenGrips in the domain.
    var GRIPS = {
        "C": [-1, 3, 2, 0, 1, 0], "D": [-1, -1, 0, 2, 3, 2], "E": [0, 2, 2, 1, 0, 0], "G": [3, 2, 0, 0, 0, 3],
        "A": [-1, 0, 2, 2, 2, 0], "Am": [-1, 0, 2, 2, 1, 0], "Dm": [-1, -1, 0, 2, 3, 1], "Em": [0, 2, 2, 0, 0, 0],
        "Cadd9": [-1, 3, 2, 0, 3, 0], "Dsus4": [-1, -1, 0, 2, 3, 3], "Asus2": [-1, 0, 2, 2, 0, 0], "Em7": [0, 2, 2, 0, 3, 0],
        "Am7": [-1, 0, 2, 0, 1, 0], "Fmaj7": [-1, -1, 3, 2, 1, 0], "A7": [-1, 0, 2, 0, 2, 0], "D7": [-1, -1, 0, 2, 1, 2],
        "E7": [0, 2, 0, 1, 0, 0], "B7": [-1, 2, 1, 2, 0, 2], "G7": [3, 2, 0, 0, 0, 1]
    };
    var OPEN = [40, 45, 50, 55, 59, 64]; // MIDI notes of the open strings E2 A2 D3 G3 B3 E4

    var ctx = null, master = null, timers = [], cache = {}, dotnet = null;

    function audio() {
        if (!ctx) {
            var AC = window.AudioContext || window.webkitAudioContext;
            if (!AC) throw new Error("Web Audio findes ikke");
            ctx = new AC();
        }
        if (ctx.state === "suspended") ctx.resume();
        return ctx;
    }

    // Karplus-Strong: a burst of noise run through a short delay line that softens itself - sounds like a plucked string.
    function pluckBuffer(midi) {
        if (cache[midi]) return cache[midi];
        var rate = ctx.sampleRate, freq = 440 * Math.pow(2, (midi - 69) / 12);
        var period = Math.round(rate / freq), length = Math.round(rate * 2.2);
        var buf = ctx.createBuffer(1, length, rate), out = buf.getChannelData(0), line = new Float32Array(period);
        for (var i = 0; i < period; i++) line[i] = Math.random() * 2 - 1;
        var p = 0, prev = 0;
        for (var n = 0; n < length; n++) {
            var cur = line[p];
            var next = 0.996 * 0.5 * (cur + prev);
            prev = cur;
            line[p] = next;
            out[n] = cur;
            p = (p + 1) % period;
        }
        cache[midi] = buf;
        return buf;
    }

    function notes(chord, capo) {
        var grip = GRIPS[chord];
        if (!grip) return [];
        var list = [];
        for (var s = 0; s < 6; s++) if (grip[s] >= 0) list.push(OPEN[s] + grip[s] + capo);
        return list;
    }

    // One stroke: strings a few ms apart, down = low to high, up = the top strings only and softer.
    function stroke(chord, capo, when, down, ringFor) {
        var list = notes(chord, capo);
        if (!down) list = list.slice(-4).reverse();
        var gap = 0.012, level = down ? 0.32 : 0.2;
        list.forEach(function (midi, i) {
            var src = ctx.createBufferSource();
            src.buffer = pluckBuffer(midi);
            var g = ctx.createGain();
            var t = when + i * gap;
            g.gain.setValueAtTime(level, t);
            g.gain.setTargetAtTime(0, t + ringFor, 0.03); // stopped by the next stroke, like a hand on the strings
            src.connect(g); g.connect(master);
            src.start(t);
            src.stop(t + ringFor + 0.3);
        });
    }

    function mark(index, i, on) {
        var el = document.getElementById("chord-" + index + "-" + i);
        if (el) el.classList.toggle("is-playing", on);
    }

    function later(fn, seconds) { timers.push(setTimeout(fn, Math.max(0, seconds * 1000))); }

    function clearMarks() {
        document.querySelectorAll(".ss-grip.is-playing").forEach(function (el) { el.classList.remove("is-playing"); });
    }

    // parts: [{ index, chords: [...] }], pattern: 8 chars of D / U / -.
    function play(parts, bpm, pattern, capo, callback) {
        stop();
        audio();
        dotnet = callback;
        master = ctx.createGain();
        master.gain.value = 0.9;
        master.connect(ctx.destination);

        var eighth = 60 / bpm / 2, start = ctx.currentTime + 0.15, t = start;
        parts.forEach(function (part) {
            later(function () { if (dotnet) dotnet.invokeMethodAsync("PartStarted", part.index); }, t - ctx.currentTime);
            part.chords.forEach(function (chord, i) {
                var barStart = t;
                later(function () { clearMarks(); mark(part.index, i, true); }, barStart - ctx.currentTime);
                for (var e = 0; e < 8; e++) {
                    var c = pattern.charAt(e);
                    if (c !== "D" && c !== "U") continue;
                    var ring = 1; // let it ring until the next stroke
                    while (e + ring < 8 && pattern.charAt(e + ring) === "-") ring++;
                    stroke(chord, capo, barStart + e * eighth, c === "D", ring * eighth);
                }
                t += 8 * eighth;
            });
        });
        later(function () {
            clearMarks();
            var cb = dotnet; dotnet = null;
            if (cb) cb.invokeMethodAsync("PlayDone");
        }, t - ctx.currentTime + 0.4);
    }

    function stop() {
        timers.forEach(clearTimeout);
        timers = [];
        clearMarks();
        if (master) { try { master.disconnect(); } catch (e) { } master = null; }
        dotnet = null;
    }

    return { play: play, stop: stop };
})();

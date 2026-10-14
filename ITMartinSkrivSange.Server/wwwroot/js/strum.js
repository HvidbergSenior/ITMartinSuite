// SkrivSange - plays chord progressions strummed, picked or both in the browser (plan 2026-10-14: Web Audio, no sound files).
// Each string is a plucked-string sound (Karplus-Strong) made once per pitch and reused. One chord = one bar of 8 eighths.
window.skrivStrum = (function () {
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

    // grip = frets per string, low E to high e, -1 = not played (sent from the domain's Grips, same as the chord box).
    function notes(grip, capo) {
        if (!grip) return [];
        var list = [];
        for (var s = 0; s < 6; s++) if (grip[s] >= 0) list.push(OPEN[s] + grip[s] + capo);
        return list;
    }

    // One picked string (fingerpicking). It rings on under the next notes, like a real picked chord.
    function pick(grip, capo, when, step, ringFor) {
        if (!grip) return;
        var played = [];
        for (var s = 0; s < 6; s++) if (grip[s] >= 0) played.push(s);
        if (!played.length) return;
        var string;
        if (step === "B") string = played[0];                                      // lowest string in the grip = the bass
        else if (step === "V") string = played.length > 1 ? played[1] : played[0]; // the next one up = alternating bass
        else string = 6 - parseInt(step, 10);                                        // 1 = high e, 2 = B, 3 = G
        if (grip[string] < 0) return;
        var src = ctx.createBufferSource();
        src.buffer = pluckBuffer(OPEN[string] + grip[string] + capo);
        var g = ctx.createGain();
        g.gain.setValueAtTime(step === "B" || step === "V" ? 0.42 : 0.34, when);
        g.gain.setTargetAtTime(0, when + ringFor, 0.08);
        src.connect(g); g.connect(master);
        src.start(when);
        src.stop(when + ringFor + 0.5);
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

    // parts: [{ index, chords: [[frets x 6], ...] }], pattern: 8 steps of D U (strum), B V 3 2 1 (pick) or - (ring).
    // loop = true: start again at the end until stop() (practice). The callback then never gets PlayDone.
    function play(parts, bpm, pattern, capo, callback, loop) {
        stop();
        audio();
        dotnet = callback;
        master = ctx.createGain();
        master.gain.value = 0.9;
        master.connect(ctx.destination);
        pass(parts, bpm, pattern, capo, !!loop);
    }

    function pass(parts, bpm, pattern, capo, loop) {

        var eighth = 60 / bpm / 2, start = ctx.currentTime + 0.15, t = start;
        parts.forEach(function (part) {
            later(function () { if (dotnet) dotnet.invokeMethodAsync("PartStarted", part.index); }, t - ctx.currentTime);
            part.chords.forEach(function (chord, i) {
                var barStart = t;
                later(function () { clearMarks(); mark(part.index, i, true); }, barStart - ctx.currentTime);
                for (var e = 0; e < 8; e++) {
                    var c = pattern.charAt(e);
                    if (c === "D" || c === "U") {
                        var ring = 1; // a strum rings until the next step, then the hand stops it
                        while (e + ring < 8 && pattern.charAt(e + ring) === "-") ring++;
                        stroke(chord, capo, barStart + e * eighth, c === "D", ring * eighth);
                    } else if ("BV321".indexOf(c) >= 0) {
                        // a picked string rings to the end of the bar, or until the next strum
                        var until = e + 1;
                        while (until < 8 && "DU".indexOf(pattern.charAt(until)) < 0) until++;
                        pick(chord, capo, barStart + e * eighth, c, (until - e) * eighth);
                    }
                }
                t += 8 * eighth;
            });
        });
        if (loop) {
            // Schedule the next round just before this one ends, so there is no gap.
            later(function () { if (master) pass(parts, bpm, pattern, capo, true); }, t - ctx.currentTime - 0.1);
            return;
        }
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

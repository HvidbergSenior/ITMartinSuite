// Web Audio synthesis, same technique as ITMartinKaraoke.Server's percussion
// pad (karaoke.js's noise-burst + pitched-thump drum hits).
//
// This is Blazor SERVER, not WASM - an @onclick handler round-trips over
// SignalR to the server and back before any JS interop call fires, and the
// Reveal-phase sequence plays from a server-pushed broadcast with no click
// behind it at all. Neither counts as a "direct user gesture" to the
// browser's autoplay policy, so just calling ctx().resume() lazily inside
// playTone (the original approach) silently produced no sound on strict
// browsers (mobile Safari especially) even though the JS never threw.
// initUnlock() attaches a RAW, non-Blazor listener that creates/resumes the
// AudioContext synchronously on the very first tap anywhere on the page
// (e.g. tapping "Start" itself) - once unlocked, later calls from async
// server-driven code work fine, since the gesture requirement only gates
// the initial resume, not every individual sound after that.
window.rewlhul = (function () {

    var _ctx = null;
    function ctx() {
        _ctx = _ctx || new (window.AudioContext || window.webkitAudioContext)();
        _ctx.resume();
        return _ctx;
    }

    var _unlocked = false;
    function initUnlock() {
        if (_unlocked) return;
        _unlocked = true;
        var unlock = function () { ctx(); };
        document.addEventListener("pointerdown", unlock, { once: true, capture: true });
        document.addEventListener("touchstart", unlock, { once: true, capture: true });
        document.addEventListener("click", unlock, { once: true, capture: true });
    }

    function noiseHit(c, now, filterType, filterFreq, peakGain, duration) {
        var bufferSize = c.sampleRate * duration;
        var buffer = c.createBuffer(1, bufferSize, c.sampleRate);
        var data = buffer.getChannelData(0);
        for (var i = 0; i < bufferSize; i++) data[i] = (Math.random() * 2 - 1) * (1 - i / bufferSize);
        var noise = c.createBufferSource();
        noise.buffer = buffer;
        var filter = c.createBiquadFilter();
        filter.type = filterType;
        filter.frequency.value = filterFreq;
        var gain = c.createGain();
        gain.gain.setValueAtTime(peakGain, now);
        gain.gain.exponentialRampToValueAtTime(0.001, now + duration);
        noise.connect(filter).connect(gain).connect(c.destination);
        noise.start(now);
    }

    function thump(c, now, startFreq, endFreq, duration) {
        var osc = c.createOscillator();
        var gain = c.createGain();
        osc.type = "sine";
        osc.frequency.setValueAtTime(startFreq, now);
        osc.frequency.exponentialRampToValueAtTime(endFreq, now + duration);
        gain.gain.setValueAtTime(1, now);
        gain.gain.exponentialRampToValueAtTime(0.001, now + duration);
        osc.connect(gain).connect(c.destination);
        osc.start(now);
        osc.stop(now + duration + 0.02);
    }

    // Low sawtooth with a wobbling pitch + irregular gain envelope, filtered
    // to sound raspy rather than a clean tone - a comedic fart, not a musical
    // note like the four drum pads.
    function fart() {
        var c = ctx();
        var now = c.currentTime;
        var duration = 0.7 + Math.random() * 0.3;

        var osc = c.createOscillator();
        osc.type = "sawtooth";
        osc.frequency.setValueAtTime(140, now);
        osc.frequency.exponentialRampToValueAtTime(60, now + duration);

        // Wobble the pitch a few times over the note for a "sputtering" feel.
        var wobble = c.createOscillator();
        wobble.type = "sine";
        wobble.frequency.value = 18 + Math.random() * 6;
        var wobbleGain = c.createGain();
        wobbleGain.gain.value = 25;
        wobble.connect(wobbleGain).connect(osc.frequency);
        wobble.start(now);
        wobble.stop(now + duration);

        var filter = c.createBiquadFilter();
        filter.type = "lowpass";
        filter.frequency.value = 500;

        var gain = c.createGain();
        gain.gain.setValueAtTime(0.001, now);
        gain.gain.linearRampToValueAtTime(0.6, now + 0.03);
        gain.gain.exponentialRampToValueAtTime(0.001, now + duration);

        osc.connect(filter).connect(gain).connect(c.destination);
        osc.start(now);
        osc.stop(now + duration + 0.02);
    }

    // One distinct drum voice per pad: kick, snare, hi-hat, cowbell.
    function playTone(index) {
        var c = ctx();
        var now = c.currentTime;
        switch (index) {
            case 0: // kick
                thump(c, now, 90, 36, 0.45);
                break;
            case 1: // snare
                noiseHit(c, now, "highpass", 2500, 0.7, 0.15);
                thump(c, now, 180, 72, 0.12);
                break;
            case 2: // hi-hat
                noiseHit(c, now, "highpass", 9000, 0.45, 0.08);
                break;
            case 3: // cowbell
                [800, 540].forEach(function (freq) {
                    var osc = c.createOscillator();
                    var gain = c.createGain();
                    osc.type = "square";
                    osc.frequency.setValueAtTime(freq, now);
                    gain.gain.setValueAtTime(0.35, now);
                    gain.gain.exponentialRampToValueAtTime(0.001, now + 0.3);
                    osc.connect(gain).connect(c.destination);
                    osc.start(now);
                    osc.stop(now + 0.3);
                });
                break;
            default:
                thump(c, now, 90, 36, 0.45);
        }
    }

    function flashPad(index) {
        var el = document.querySelector('[data-pad="' + index + '"]');
        if (!el) return;
        el.classList.add("rw-pad-active");
        setTimeout(function () { el.classList.remove("rw-pad-active"); }, 400);
    }

    // Steps through a sequence with the same timing every client uses, so
    // phones physically in the same room play (roughly) together.
    function playSequence(indexes, stepMs) {
        indexes.forEach(function (idx, i) {
            setTimeout(function () {
                flashPad(idx);
                playTone(idx);
            }, i * stepMs);
        });
    }

    // Blazor Server round-trips every click over SignalR before the C#
    // handler can call back into JS - noticeable delay between the physical
    // press and the sound. This listens on the pad grid directly (raw DOM
    // event, no Blazor involved) so the sound fires the instant the button
    // is pressed, in parallel with (not waiting for) the server round-trip
    // that still submits the tap for game-state/multiplayer scoring.
    // Delegated on the grid container, not the buttons themselves, so it
    // survives Blazor re-rendering/patching the individual pad elements.
    var _tapSoundsBound = false;
    function initTapSounds() {
        if (_tapSoundsBound) return;
        _tapSoundsBound = true;
        document.addEventListener("pointerdown", function (e) {
            var pad = e.target.closest("[data-pad]");
            if (!pad || pad.disabled) return;
            playTone(parseInt(pad.getAttribute("data-pad"), 10));
        }, true);
    }

    return {
        initUnlock: initUnlock,
        initTapSounds: initTapSounds,
        playTone: playTone,
        playSequence: playSequence,
        fart: fart
    };
})();

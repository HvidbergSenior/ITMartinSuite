window.karaoke = (function () {

    // ── Local (ripped-CD) audio playback + lyric sync ──────────────────────
    // Mirrors spotify.js's getState/startLyricSync/stopLyricSync shape so
    // Stage.razor can drive either source through the same highlighting code.

    var _audio = null;
    var _lyricSyncTimer = null;
    var _lyricSyncScrollHandler = null;
    var _lastManualScroll = 0;
    var MANUAL_SCROLL_PAUSE_MS = 2500;

    function playLocal(url) {
        stopLocal();
        _audio = new Audio(url);
        _audio.play().catch(function (e) { console.error("Local playback failed", e); });
        return Promise.resolve();
    }

    function pauseLocal() {
        if (_audio) _audio.pause();
    }

    function stopLocal() {
        if (_audio) { _audio.pause(); _audio.currentTime = 0; _audio = null; }
        stopLyricSyncLocal();
    }

    function getLocalState() {
        if (!_audio) return Promise.resolve(null);
        return Promise.resolve({
            positionMs: (_audio.currentTime || 0) * 1000,
            durationMs: (_audio.duration || 0) * 1000,
            paused: _audio.paused
        });
    }

    function startLyricSyncLocal(containerId) {
        stopLyricSyncLocal();
        var container = document.getElementById(containerId);
        if (!container) return;
        var lines = Array.prototype.slice.call(container.querySelectorAll("[data-time]"));
        if (lines.length === 0) return;

        _lastManualScroll = 0;
        _lyricSyncScrollHandler = function () { _lastManualScroll = Date.now(); };
        window.addEventListener("wheel", _lyricSyncScrollHandler, { passive: true });
        window.addEventListener("touchmove", _lyricSyncScrollHandler, { passive: true });

        _lyricSyncTimer = setInterval(function () {
            if (!_audio) return;
            var positionSec = _audio.currentTime || 0;
            var activeIndex = -1;
            for (var i = 0; i < lines.length; i++) {
                if (parseFloat(lines[i].dataset.time) <= positionSec) activeIndex = i;
                else break;
            }
            for (var j = 0; j < lines.length; j++) {
                lines[j].classList.toggle("active", j === activeIndex);
            }
            var recentlyScrolledByHand = (Date.now() - _lastManualScroll) < MANUAL_SCROLL_PAUSE_MS;
            if (activeIndex >= 0 && !recentlyScrolledByHand) {
                lines[activeIndex].scrollIntoView({ block: "center", behavior: "smooth" });
            }
        }, 300);
    }

    function stopLyricSyncLocal() {
        if (_lyricSyncTimer) { clearInterval(_lyricSyncTimer); _lyricSyncTimer = null; }
        if (_lyricSyncScrollHandler) {
            window.removeEventListener("wheel", _lyricSyncScrollHandler);
            window.removeEventListener("touchmove", _lyricSyncScrollHandler);
            _lyricSyncScrollHandler = null;
        }
    }

    // ── Performance recording (mic + optional camera) ──────────────────────
    // Deliberately does NOT isolate/clean the mic signal the way MusikStudio's
    // overdub recording does - this is meant to capture the room (singer,
    // backing music playing on speakers, guitar amp, bucket drumming, all of
    // it together), not a clean solo vocal take.

    var _mediaRecorder = null;
    var _chunks = [];
    var _stream = null;

    function listAudioInputs() {
        return navigator.mediaDevices.enumerateDevices().then(function (devices) {
            return devices.filter(function (d) { return d.kind === "audioinput"; })
                .map(function (d) { return { deviceId: d.deviceId, label: d.label || "Mikrofon" }; });
        });
    }

    function startRecording(videoMode, deviceId) {
        _chunks = [];
        var audioConstraints = deviceId ? { deviceId: { exact: deviceId } } : true;
        var constraints = videoMode
            ? { audio: audioConstraints, video: { width: { ideal: 1280 }, height: { ideal: 720 } } }
            : { audio: audioConstraints, video: false };

        return navigator.mediaDevices.getUserMedia(constraints).then(function (stream) {
            _stream = stream;

            if (videoMode) {
                var preview = document.getElementById("ka-rec-preview");
                if (preview) {
                    preview.srcObject = stream;
                    preview.muted = true;
                    preview.play().catch(function () {});
                }
            }

            var mimeType = videoMode ? "video/webm;codecs=vp8,opus" : "audio/webm";
            if (!MediaRecorder.isTypeSupported(mimeType)) mimeType = videoMode ? "video/webm" : "";

            _mediaRecorder = new MediaRecorder(stream, mimeType ? { mimeType: mimeType } : {});
            _mediaRecorder.ondataavailable = function (e) {
                if (e.data && e.data.size > 0) _chunks.push(e.data);
            };
            _mediaRecorder.start(500);
        });
    }

    function stopRecording(queueEntryId, label) {
        return new Promise(function (resolve) {
            if (!_mediaRecorder) { resolve(false); return; }

            var recorder = _mediaRecorder;
            var mimeType = recorder.mimeType || "audio/webm";
            _mediaRecorder = null;

            var preview = document.getElementById("ka-rec-preview");
            if (preview) preview.srcObject = null;

            recorder.onstop = function () {
                var blob = new Blob(_chunks, { type: mimeType });
                _chunks = [];
                if (_stream) { _stream.getTracks().forEach(function (t) { t.stop(); }); _stream = null; }

                if (blob.size === 0) { resolve(false); return; }

                fetch("/api/recording/" + queueEntryId + "?label=" + encodeURIComponent(label || "gæst"), {
                    method: "POST",
                    headers: { "Content-Type": mimeType },
                    body: blob
                }).then(function () { resolve(true); })
                  .catch(function (err) { console.error("Upload failed", err); resolve(false); });
            };
            recorder.stop();
        });
    }

    // ── Percussion pad ───────────────────────────────────────────────────
    // Synthesized with the Web Audio API rather than sample files - a bucket,
    // a table, whatever's at hand already makes the "real" percussion sound
    // in the room; these are just a rhythmic backup for whoever doesn't have
    // something to bang on.

    var _padCtx = null;
    function padCtx() {
        _padCtx = _padCtx || new (window.AudioContext || window.webkitAudioContext)();
        _padCtx.resume();
        return _padCtx;
    }

    function playPad(kind) {
        var ctx = padCtx();
        var now = ctx.currentTime;

        if (kind === "clap" || kind === "shaker") {
            var bufferSize = ctx.sampleRate * (kind === "clap" ? 0.2 : 0.15);
            var buffer = ctx.createBuffer(1, bufferSize, ctx.sampleRate);
            var data = buffer.getChannelData(0);
            for (var i = 0; i < bufferSize; i++) data[i] = (Math.random() * 2 - 1) * (1 - i / bufferSize);
            var noise = ctx.createBufferSource();
            noise.buffer = buffer;
            var filter = ctx.createBiquadFilter();
            filter.type = kind === "clap" ? "bandpass" : "highpass";
            filter.frequency.value = kind === "clap" ? 1200 : 6000;
            var gain = ctx.createGain();
            gain.gain.setValueAtTime(kind === "clap" ? 0.9 : 0.5, now);
            gain.gain.exponentialRampToValueAtTime(0.001, now + (kind === "clap" ? 0.2 : 0.15));
            noise.connect(filter).connect(gain).connect(ctx.destination);
            noise.start(now);
            return;
        }

        // drum / tom: a simple pitched thump
        var osc = ctx.createOscillator();
        var gain2 = ctx.createGain();
        osc.type = "sine";
        var startFreq = kind === "drum" ? 150 : 300;
        osc.frequency.setValueAtTime(startFreq, now);
        osc.frequency.exponentialRampToValueAtTime(startFreq * 0.4, now + 0.25);
        gain2.gain.setValueAtTime(1, now);
        gain2.gain.exponentialRampToValueAtTime(0.001, now + 0.35);
        osc.connect(gain2).connect(ctx.destination);
        osc.start(now);
        osc.stop(now + 0.4);
    }

    return {
        playLocal: playLocal,
        pauseLocal: pauseLocal,
        stopLocal: stopLocal,
        getLocalState: getLocalState,
        startLyricSyncLocal: startLyricSyncLocal,
        stopLyricSyncLocal: stopLyricSyncLocal,
        listAudioInputs: listAudioInputs,
        startRecording: startRecording,
        stopRecording: stopRecording,
        playPad: playPad
    };

})();

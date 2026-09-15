window.studio = (function() {

    var _mediaRecorder = null;
    var _chunks = [];
    var _micStream = null;
    var _camStream = null;
    var _songKey = null;
    var _videoMode = false;
    var _section = null; // optional lyric-section label for "record one verse at a time"
    var _mixAudios = [];
    var _mixVolume = 0.5; // default lower than full - reduces speaker bleed into the mic when no headphones are used
    var _audioCtx = null;
    var _micGainNode = null;
    var _micGain = 1.5; // default boost - "record me higher"

    // ── Recording ──────────────────────────────────────────────────────────────

    function startRecording(songKey, videoMode, section) {
        _songKey = songKey;
        _videoMode = !!videoMode;
        _section = section || null;
        _chunks = [];

        // Explicitly OFF: these are WebRTC voice-call constraints (tuned for
        // laptop mics in noisy rooms), and on a real condenser/USB mic they
        // actively hurt quality - pumping/gating artifacts that read as
        // "background noise", and a processed, degraded vocal tone. The
        // correct way to avoid backing-track bleed during overdub is
        // headphones (see the hint next to the "optag med sangen" button),
        // not browser-side audio processing on the mic signal.
        var micConstraints = {
            echoCancellation: false,
            noiseSuppression: false,
            autoGainControl: false
        };
        var constraints = _videoMode
            ? { audio: micConstraints, video: { width: { ideal: 1280 }, height: { ideal: 720 } } }
            : { audio: micConstraints, video: false };

        return navigator.mediaDevices.getUserMedia(constraints)
            .then(function(stream) {
                _micStream = stream;

                if (_videoMode) {
                    var preview = document.getElementById("rec-preview");
                    if (preview) {
                        preview.srcObject = stream;
                        preview.muted = true;
                        preview.play().catch(function() {});
                    }
                }

                // A plain linear gain boost on the raw mic signal - not the
                // same thing as the AGC/noise-suppression left off above.
                // Those are adaptive/dynamic processing that degrades tone;
                // this is just "make the whole recorded signal louder",
                // which is what "record me higher" actually needs.
                _audioCtx = _audioCtx || new (window.AudioContext || window.webkitAudioContext)();
                // Reused across recordings, so it can be auto-suspended by the
                // browser (tab lost focus, idle timeout, etc.) between takes -
                // e.g. while you're listening back to the previous one. If
                // suspended, node creation/connection still succeeds silently,
                // but no audio actually flows through, producing a 0-byte
                // recording with no visible error. resume() is always safe to
                // call even if already running.
                _audioCtx.resume();
                var micSource = _audioCtx.createMediaStreamSource(new MediaStream(stream.getAudioTracks()));
                _micGainNode = _audioCtx.createGain();
                _micGainNode.gain.value = _micGain;
                var dest = _audioCtx.createMediaStreamDestination();
                micSource.connect(_micGainNode).connect(dest);

                var recordStream = _videoMode
                    ? new MediaStream(stream.getVideoTracks().concat(dest.stream.getAudioTracks()))
                    : dest.stream;

                var mimeType = _videoMode ? "video/webm;codecs=vp8,opus" : "audio/webm";
                if (!MediaRecorder.isTypeSupported(mimeType)) {
                    mimeType = "video/webm";
                }
                if (!MediaRecorder.isTypeSupported(mimeType)) {
                    mimeType = "";
                }

                // Default Opus bitrate leans low (voice-call territory) -
                // 192kbps gives a real condenser mic's vocal recording
                // noticeably more headroom/clarity than the browser default.
                var opts = mimeType ? { mimeType: mimeType, audioBitsPerSecond: 192000 } : {};
                _mediaRecorder = new MediaRecorder(recordStream, opts);

                _mediaRecorder.ondataavailable = function(e) {
                    if (e.data && e.data.size > 0) _chunks.push(e.data);
                };

                _mediaRecorder.start(500);
                console.log("Recording started", _videoMode ? "video" : "audio", mimeType);
            })
            .catch(function(err) {
                console.error("Media access failed", err.name, err.message);
                throw err;
            });
    }

    function stopMix() {
        _mixAudios.forEach(function(a) { try { a.pause(); a.currentTime = 0; } catch(e) {} });
        _mixAudios = [];
    }

    // Boosts your recorded mic level (voice + guitar picked up together, e.g.
    // by a HyperX QuadCast) - takes effect immediately if a recording is
    // already running, and carries over as the default for the next one.
    function setMicGain(gain) {
        _micGain = Math.max(0.1, Math.min(4, gain));
        if (_micGainNode) _micGainNode.gain.value = _micGain;
    }

    // Volume of the reference/backing track only - this plays through your own
    // speakers/headphones for timing, it is never part of the recorded file
    // (recording is mic-only, see startRecording above). Turning this down
    // makes it easier to hear your own playing while performing; it does not
    // change the balance of anything already in the recording.
    function setMixVolume(vol) {
        _mixVolume = Math.max(0, Math.min(1, vol));
        _mixAudios.forEach(function(a) { a.volume = _mixVolume; });
    }

    function playMix(urls) {
        stopMix();
        _mixAudios = urls.map(function(url) {
            var a = new Audio(url);
            a.volume = _mixVolume;
            a.play().catch(function(e) { console.error("Mix playback failed", e); });
            return a;
        });
    }

    function startOverdub(songKey, playUrl, videoMode, startSeconds) {
        stopMix();
        var playback = new Audio(playUrl);
        playback.volume = _mixVolume;
        playback.preload = "auto"; // start buffering now, in parallel with mic setup below
        // Seeking only works reliably once metadata's loaded - setting
        // currentTime immediately after `new Audio()` is a no-op in most
        // browsers since duration/seekable range aren't known yet.
        var seek = startSeconds || 0;
        if (seek > 0) {
            playback.addEventListener('loadedmetadata', function () {
                try { playback.currentTime = seek; } catch (e) {}
            }, { once: true });
        }
        // Recording must be fully ready (mic permission granted, AudioContext
        // wired up - startRecording's getUserMedia round-trip is not
        // instant) BEFORE the backing track starts playing. Previously
        // playback started first and recording began however long that
        // setup took afterwards, so everything you sang got captured that
        // much late relative to a fresh instrumental at mixdown time -
        // sounded "not synced". Starting the recorder first means any
        // leftover gap is silence at the top of the take (harmless,
        // trimmable) instead of losing real singing.
        return startRecording(songKey, videoMode).then(function () {
            _mixAudios = [playback];
            playback.play().catch(function(e) { console.error("Overdub playback failed", e); });
        });
    }

    function stopRecording() {
        stopMix();
        return new Promise(function(resolve) {
            if (!_mediaRecorder) { resolve(); return; }

            // Capture before nulling to avoid the null reference in onstop
            var recorder = _mediaRecorder;
            var capturedMime = recorder.mimeType || (_videoMode ? "video/webm" : "audio/webm");
            var capturedKey = _songKey;
            var capturedSection = _section;
            _mediaRecorder = null;
            _section = null;

            // Stop video preview
            var preview = document.getElementById("rec-preview");
            if (preview) preview.srcObject = null;

            recorder.onstop = function() {
                var blob = new Blob(_chunks, { type: capturedMime });
                _chunks = [];

                if (_micStream) {
                    _micStream.getTracks().forEach(function(t) { t.stop(); });
                    _micStream = null;
                }

                if (!capturedKey || blob.size === 0) { resolve(); return; }

                var url = "/api/recording/" + encodeURIComponent(capturedKey);
                if (capturedSection) url += "?section=" + encodeURIComponent(capturedSection);

                fetch(url, {
                    method: "POST",
                    headers: { "Content-Type": capturedMime },
                    body: blob
                })
                .then(function(r) { return r.json(); })
                .then(function(data) {
                    console.log("Recording saved", data.path);
                    resolve();
                })
                .catch(function(err) {
                    console.error("Upload failed", err);
                    resolve();
                });
            };

            recorder.stop();
        });
    }

    // ── Camera (for photo capture) ─────────────────────────────────────────────

    function startCamera(videoId) {
        var video = document.getElementById(videoId);
        if (!video) return Promise.reject(new Error("Video element not found: " + videoId));

        stopCamera();

        var constraintSets = [
            { video: { facingMode: { ideal: "environment" }, width: { ideal: 1920 }, height: { ideal: 1080 } }, audio: false },
            { video: { facingMode: "environment" }, audio: false },
            { video: true, audio: false }
        ];

        function tryNext(i) {
            if (i >= constraintSets.length) {
                return Promise.reject(new Error("Could not access camera"));
            }
            return _getUserMedia(constraintSets[i]).then(function(stream) {
                _camStream = stream;
                video.srcObject = stream;
                video.setAttribute("autoplay", "");
                video.setAttribute("muted", "");
                video.setAttribute("playsinline", "");
                video.setAttribute("webkit-playsinline", "");
                video.autoplay = true;
                video.muted = true;
                video.playsInline = true;
                return video.play().catch(function() {});
            }).catch(function(e) {
                console.warn("Camera attempt " + i + " failed", e.name);
                return tryNext(i + 1);
            });
        }

        return tryNext(0);
    }

    function stopCamera() {
        if (_camStream) {
            _camStream.getTracks().forEach(function(t) { t.stop(); });
            _camStream = null;
        }
    }

    function capturePhoto(videoId) {
        var video = document.getElementById(videoId);
        if (!video) return "";

        return new Promise(function(resolve) {
            setTimeout(function() {
                var canvas = document.createElement("canvas");
                var w = video.videoWidth || 640;
                var h = video.videoHeight || 480;

                var MAX = 1600;
                if (w > MAX || h > MAX) {
                    var r = Math.min(MAX / w, MAX / h);
                    w = Math.round(w * r);
                    h = Math.round(h * r);
                }

                canvas.width = w;
                canvas.height = h;
                canvas.getContext("2d").drawImage(video, 0, 0, w, h);

                var base64 = canvas.toDataURL("image/jpeg", 0.88);
                var stripped = base64.replace("data:image/jpeg;base64,", "");
                resolve(stripped);
            }, 300);
        });
    }

    // ── Sketch capture (Skriv sang "hum an idea") ────────────────────────────
    // Deliberately separate state from _mediaRecorder/_chunks above - a
    // sketch is a short scratch clip for the from-scratch flow, not a take,
    // and must never collide with the full take-recording flow's state.

    var _sketchRecorder = null;
    var _sketchChunks = [];
    var _sketchStream = null;
    var _sketchKey = null;

    function startSketch(songKey) {
        _sketchKey = songKey;
        _sketchChunks = [];
        return _getUserMedia({ audio: { echoCancellation: false, noiseSuppression: false, autoGainControl: false } })
            .then(function(stream) {
                _sketchStream = stream;
                var mimeType = (window.MediaRecorder && MediaRecorder.isTypeSupported("audio/webm")) ? "audio/webm" : "";
                _sketchRecorder = new MediaRecorder(stream, mimeType ? { mimeType: mimeType } : {});
                _sketchRecorder.ondataavailable = function(e) { if (e.data && e.data.size > 0) _sketchChunks.push(e.data); };
                _sketchRecorder.start(500);
            });
    }

    function stopSketch() {
        return new Promise(function(resolve) {
            if (!_sketchRecorder) { resolve(); return; }

            var recorder = _sketchRecorder;
            var capturedMime = recorder.mimeType || "audio/webm";
            var capturedKey = _sketchKey;
            _sketchRecorder = null;

            recorder.onstop = function() {
                var blob = new Blob(_sketchChunks, { type: capturedMime });
                _sketchChunks = [];

                if (_sketchStream) {
                    _sketchStream.getTracks().forEach(function(t) { t.stop(); });
                    _sketchStream = null;
                }

                if (!capturedKey || blob.size === 0) { resolve(); return; }

                fetch("/api/sketch/" + encodeURIComponent(capturedKey), {
                    method: "POST",
                    headers: { "Content-Type": capturedMime },
                    body: blob
                })
                .then(function(r) { return r.json(); })
                .then(function() { resolve(); })
                .catch(function(err) { console.error("Sketch upload failed", err); resolve(); });
            };

            recorder.stop();
        });
    }

    // ── Misc ──────────────────────────────────────────────────────────────────

    function scrollToId(id) {
        var el = document.getElementById(id);
        if (el) el.scrollIntoView({ behavior: "smooth", block: "start" });
    }

    function _getUserMedia(constraints) {
        if (navigator.mediaDevices && navigator.mediaDevices.getUserMedia) {
            return navigator.mediaDevices.getUserMedia(constraints);
        }
        var legacy = navigator.getUserMedia || navigator.webkitGetUserMedia || navigator.mozGetUserMedia;
        if (legacy) {
            return new Promise(function(resolve, reject) {
                legacy.call(navigator, constraints, resolve, reject);
            });
        }
        return Promise.reject(new Error("getUserMedia not supported"));
    }

    // Reads back an <audio>/<video> element's current scrub position - used
    // by the "set section start time" button so the user can find the spot
    // by ear in a normal player instead of typing seconds by hand.
    function getAudioCurrentTime(elementId) {
        var el = document.getElementById(elementId);
        return el ? el.currentTime : 0;
    }

    return {
        startRecording: startRecording,
        stopRecording: stopRecording,
        startOverdub: startOverdub,
        getAudioCurrentTime: getAudioCurrentTime,
        playMix: playMix,
        stopMix: stopMix,
        setMixVolume: setMixVolume,
        setMicGain: setMicGain,
        startCamera: startCamera,
        stopCamera: stopCamera,
        capturePhoto: capturePhoto,
        startSketch: startSketch,
        stopSketch: stopSketch,
        scrollToId: scrollToId
    };

})();
